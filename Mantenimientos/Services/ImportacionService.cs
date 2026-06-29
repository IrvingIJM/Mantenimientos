using Mantenimientos.Data;
using Mantenimientos.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Mantenimientos.Services
{
    /// <summary>
    /// Servicio en segundo plano que importa automáticamente las sucursales
    /// activas de Iker que aún no tienen registro en Seguimientos para el
    /// periodo actual.
    ///
    /// Se ejecuta:
    ///   1. Inmediatamente al arrancar la aplicación.
    ///   2. Cada N minutos (configurable en appsettings.json → "ImportacionAutomatica:IntervaloMinutos").
    ///
    /// Usa NOT EXISTS, por lo que es idempotente: correr varias veces no
    /// duplica registros. Solo inserta sucursales que genuinamente son nuevas
    /// o que no tenían registro para el periodo actual.
    /// </summary>
    public class ImportacionAutomaticaService : BackgroundService
    {
        // IServiceProvider porque BackgroundService es Singleton, pero
        // ApplicationDbContext y PeriodoService son Scoped — necesitamos
        // crear un scope en cada ejecución.
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImportacionAutomaticaService> _logger;
        private readonly TimeSpan _intervalo;

        public ImportacionAutomaticaService(
            IServiceProvider serviceProvider,
            ILogger<ImportacionAutomaticaService> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Intervalo configurable; si no está en appsettings usa 60 minutos.
            int minutos = config.GetValue<int>(
                "ImportacionAutomatica:IntervaloMinutos", 60);
            _intervalo = TimeSpan.FromMinutes(minutos);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ciclo principal
        // ─────────────────────────────────────────────────────────────────────

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "ImportacionAutomatica: servicio iniciado — intervalo {Min} min.",
                _intervalo.TotalMinutes);

            // Ejecución inicial al arrancar (sin esperar el primer tick)
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

        // ─────────────────────────────────────────────────────────────────────
        // Lógica de importación
        // ─────────────────────────────────────────────────────────────────────

        private async Task EjecutarImportAsync()
        {
            try
            {
                // Crear scope para obtener servicios Scoped
                using var scope = _serviceProvider.CreateScope();
                var periodoService = scope.ServiceProvider.GetRequiredService<PeriodoService>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                int periodoActual = await periodoService.ObtenerPeriodoActualAsync();

                // Inserta SOLO las sucursales activas de Iker que NO tienen
                // registro en Seguimientos para el periodo actual.
                // Cubre dos casos automáticamente:
                //   a) Sucursales de una ruta nueva (ej. ruta 8 recién agregada).
                //   b) Sucursales existentes que fueron activadas en Iker.
                const string sql = @"
                    INSERT INTO mttos.dbo.Seguimientos (CLV_SUC, ID_PERIODO)
                    SELECT suc.CLV_SUC, @PeriodoActual
                    FROM   Iker.dbo.Sucursales AS suc
                    WHERE  suc.ACTIVO = 1
                      AND  NOT EXISTS (
                               SELECT 1
                               FROM   mttos.dbo.Seguimientos AS s
                               WHERE  s.CLV_SUC    = suc.CLV_SUC
                                 AND  s.ID_PERIODO = @PeriodoActual
                           );";

                int insertados = await context.Database.ExecuteSqlRawAsync(
                    sql,
                    new SqlParameter("@PeriodoActual", periodoActual));

                if (insertados > 0)
                {
                    _logger.LogInformation(
                        "ImportacionAutomatica: {N} sucursal(es) nueva(s) importada(s) " +
                        "para Periodo {P} — {Hora}",
                        insertados, periodoActual,
                        DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                }
                else
                {
                    _logger.LogDebug(
                        "ImportacionAutomatica: sin cambios para Periodo {P} — {Hora}",
                        periodoActual,
                        DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                }
            }
            catch (Exception ex)
            {
                // Loguear sin romper el ciclo — el servicio sigue intentando
                // en el próximo tick aunque esta ejecución falle.
                _logger.LogError(ex,
                    "ImportacionAutomatica: error durante la importación. " +
                    "Se reintentará en {Min} minutos.", _intervalo.TotalMinutes);
            }
        }
    }
}
