using Microsoft.Data.SqlClient;
using System.Data;

namespace Mantenimientos.Services
{
    /// <summary>
    /// Obtiene el periodo actual desde la base de datos de la empresa
    /// mediante un procedimiento almacenado configurado en appsettings.json ("PeriodoActualSP").
    /// El resultado se cachea 30 minutos para no golpear el SP en cada request.
    /// </summary>
    public class PeriodoService
    {
        private readonly string _connIker;
        private readonly string _spName;
        private readonly ILogger<PeriodoService> _logger;

        // Cache estático compartido en el proceso — se invalida solo al reiniciar la app
        // o al llamar InvalidarCache().
        private static int? _cache;
        private static DateTime _expiry = DateTime.MinValue;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public PeriodoService(IConfiguration config, ILogger<PeriodoService> logger)
        {
            _connIker = config.GetConnectionString("DatabaseConnection")
                ?? throw new InvalidOperationException(
                    "No se encontró 'DatabaseConnection' en appsettings.json.");

            // Nombre del SP configurado en appsettings.json → "PeriodoActualSP"
            // Si no está definido usa "dbo.sp_PeriodoActual" por defecto.
            _spName = config["PeriodoActualSP"] ?? "dbo.sp_PeriodoActual";
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // API pública
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve el número entero del periodo actual.
        /// Llama al SP configurado; si falla, usa MAX(id_periodo) de DBICET como fallback.
        /// </summary>
        public async Task<int> ObtenerPeriodoActualAsync()
        {
            // Fast path — sin lock si el caché es válido
            if (_cache.HasValue && DateTime.Now < _expiry)
                return _cache.Value;

            await _lock.WaitAsync();
            try
            {
                // Double-check después de adquirir el lock
                if (_cache.HasValue && DateTime.Now < _expiry)
                    return _cache.Value;

                int periodo = await LlamarSpPeriodoAsync();

                _cache = periodo;
                _expiry = DateTime.Now.AddMinutes(30);

                _logger.LogInformation(
                    "PeriodoService: periodo actual = {Periodo} (expira {Expiry:HH:mm:ss})",
                    periodo, _expiry);

                return periodo;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Devuelve exactamente dos opciones para el dropdown:
        ///   - Periodo actual   (marcado como EsActual = true)
        ///   - Periodo anterior (marcado como EsActual = false)
        /// </summary>
        public async Task<List<PeriodoDto>> ObtenerPeriodosDisponiblesAsync()
        {
            int actual = await ObtenerPeriodoActualAsync();
            int anterior = actual - 1;

            return new List<PeriodoDto>
            {
                new() { Id = actual,   Nombre = $"Periodo {actual} — Actual",    EsActual = true  },
                new() { Id = anterior, Nombre = $"Periodo {anterior} — Anterior", EsActual = false }
            };
        }

        /// <summary>
        /// Invalida el caché manualmente.
        /// Útil si se sabe que el periodo cambió sin reiniciar la app.
        /// </summary>
        public static void InvalidarCache()
        {
            _cache = null;
            _expiry = DateTime.MinValue;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers privados
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ejecuta el procedimiento almacenado y lee el primer valor entero devuelto.
        /// Si el SP falla pasa al fallback.
        /// </summary>
        private async Task<int> LlamarSpPeriodoAsync()
        {
            try
            {
                await using var conn = new SqlConnection(_connIker);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(_spName, conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 10
                };

                // El SP debe devolver un único valor entero con el periodo actual.
                // Puede ser mediante SELECT o parámetro OUTPUT; aquí se lee
                // la primera columna del primer registro con ExecuteScalar.
                var resultado = await cmd.ExecuteScalarAsync();

                if (resultado is null or DBNull)
                    throw new InvalidOperationException(
                        $"El SP '{_spName}' no devolvió ningún valor.");

                return Convert.ToInt32(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PeriodoService: error al llamar SP '{Sp}'. Usando fallback (MAX id_periodo).",
                    _spName);

                return await FallbackMaxPeriodoAsync();
            }
        }

        /// <summary>
        /// Fallback: obtiene el periodo máximo de DBICET cuando el SP no responde.
        /// Usa DatabaseConnection (Iker) directamente, sin prefijo de BD cruzada.
        /// </summary>
        private async Task<int> FallbackMaxPeriodoAsync()
        {
            await using var conn = new SqlConnection(_connIker);
            await conn.OpenAsync();

            const string sql = @"
                SELECT MAX(id_periodo)
                FROM   dbo.DBICET
                WHERE  id_periodo IS NOT NULL";

            await using var cmd = new SqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();

            if (result is null or DBNull)
                throw new InvalidOperationException(
                    "No se pudo determinar el periodo actual desde DBICET.");

            return Convert.ToInt32(result);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTO
    // ─────────────────────────────────────────────────────────────────────────

    public class PeriodoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>True = periodo actual; False = periodo anterior.</summary>
        public bool EsActual { get; set; }
    }
}
