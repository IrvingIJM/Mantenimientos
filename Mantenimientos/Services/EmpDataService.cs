using Microsoft.Data.SqlClient;

namespace Mantenimientos.Services
{
    public class EmpDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<EmpDataService> _logger;

        private static readonly DateTime FechaDefault = new(1900, 1, 1);

        public EmpDataService(
            IConfiguration configuracion,
            ILogger<EmpDataService> logger)
        {
            _connectionString = configuracion.GetConnectionString("DatabaseConnection")
                ?? throw new InvalidOperationException(
                    "No se encontró 'DatabaseConnection' en appsettings.json");
            _logger = logger;
        }

        // Obtener rutas desde BD
        public async Task<List<string>> ObtenerRutasAsync()
        {
            var rutas = new List<string>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT CAST(RUTA AS NVARCHAR(50))
                    FROM iker
                    WHERE RUTA IS NOT NULL
                    ORDER BY 1";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    rutas.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rutas desde la BD empresa.");
            }
            return rutas;
        }

        // Obtener sucursales de una ruta desde BD
        public async Task<List<string>> ObtenerSucursales(string ruta)
        {
            var sucursales = new List<string>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT SUCURSAL
                    FROM iker
                    WHERE RUTA = @Ruta
                      AND SUCURSAL IS NOT NULL
                    ORDER BY SUCURSAL";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Ruta", ruta);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    sucursales.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener sucursales para ruta {Ruta}.", ruta);
            }
            return sucursales;
        }

        // Obtener fechas estimadas desde mttos para una ruta y sucursal
        public async Task<FechasEstimadasDto?> ObtenerFechasEstimadasAsync(string ruta, string sucursal)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT TOP 1
                        FECHA_INI_ES,
                        FECHA_FIN_ES
                    FROM mttos
                    WHERE RUTA     = @Ruta
                      AND SUCURSAL = @Sucursal";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Ruta", ruta);
                cmd.Parameters.AddWithValue("@Sucursal", sucursal);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new FechasEstimadasDto
                    {
                        FechaInicioEstimada = reader["FECHA_INI_ES"] as DateTime? ?? FechaDefault,
                        FechaFinEstimada = reader["FECHA_FIN_ES"] as DateTime? ?? FechaDefault
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al obtener fechas estimadas para Ruta={Ruta}, Sucursal={Sucursal}.",
                    ruta, sucursal);
            }
            return null;
        }
    }

    public class FechasEstimadasDto
    {
        public DateTime FechaInicioEstimada { get; set; }
        public DateTime FechaFinEstimada { get; set; }
    }
}
