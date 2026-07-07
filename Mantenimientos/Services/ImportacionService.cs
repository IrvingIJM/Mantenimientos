using Mantenimientos.Data;
using Mantenimientos.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Mantenimientos.Services
{
    public class ImportacionAutomaticaService : BackgroundService
    {
        // crear un scope en cada ejecución.
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImportacionAutomaticaService> _logger;
        private readonly TimeSpan _intervalo;

        public ImportacionAutomaticaService(IServiceProvider serviceProvider, ILogger<ImportacionAutomaticaService> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Intervalo configurable si no está en appsettings usa 60 minutos.
            int minutos = config.GetValue<int>("ImportacionAutomatica:IntervaloMinutos", 60);
            _intervalo = TimeSpan.FromMinutes(minutos);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Ejecución inicial al arrancar
            await EjecutarImportAsync();

            // Luego ejecutar cada N minutos mientras la app esté corriendo
            using var timer = new PeriodicTimer(_intervalo);

            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EjecutarImportAsync();
            }
            _logger.LogInformation("ImportacionAutomatica: servicio detenido.");
        }

        private async Task EjecutarImportAsync()
        {
            try
            {
                // Crear scope para obtener servicios Scoped
                using var scope = _serviceProvider.CreateScope();
                var periodoService = scope.ServiceProvider.GetRequiredService<PeriodoService>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                int periodoActual = await periodoService.ObtenerPeriodoActualAsync();

                // Inserta solo las sucursales activas que no tienen registro en Seguimientos para el periodo actual.
                const string sql = @"
                    INSERT INTO mttos.dbo.Seguimientos (CLV_SUC, ID_PERIODO)
                    SELECT suc.CLV_SUC, @PeriodoActual
                    FROM Iker.dbo.Sucursales AS suc
                    WHERE suc.ACTIVO = 1 AND NOT EXISTS ( SELECT 1
                    FROM mttos.dbo.Seguimientos AS s
                    WHERE s.CLV_SUC = suc.CLV_SUC AND s.ID_PERIODO = @PeriodoActual);";

                int insertados = await context.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@PeriodoActual", periodoActual));

                if (insertados > 0)
                {
                    _logger.LogInformation(
                        "ImportacionAutomatica: {N} sucursal(es) nueva(s) importada(s) " + "para Periodo {P} — {Hora}", insertados, periodoActual,
                        DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                }
                else
                {
                    _logger.LogDebug(
                        "ImportacionAutomatica: sin cambios para Periodo {P} — {Hora}", periodoActual,
                        DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                }
            }
            catch (Exception ex)
            {
                // Loguear sin romper el ciclo
                _logger.LogError(ex,
                    "ImportacionAutomatica: error durante la importación. " + "Se reintentará en {Min} minutos.", _intervalo.TotalMinutes);
            }
        }
    }
}
