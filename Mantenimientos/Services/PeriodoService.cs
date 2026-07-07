using Microsoft.Data.SqlClient;
using System.Data;

namespace Mantenimientos.Services
{
    public class PeriodoService
    {
        private readonly string _connDB;
        private readonly string _spName;
        private readonly ILogger<PeriodoService> _logger;
        private static int? _cache;
        private static DateTime _expiry = DateTime.MinValue;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public PeriodoService(IConfiguration config, ILogger<PeriodoService> logger)
        {
            _connDB = config.GetConnectionString("DatabaseConnection")
                ?? throw new InvalidOperationException("No se encontró 'DatabaseConnection' en appsettings.json.");

            _spName = config["PeriodoActualSP"] ?? "dbo.periodoActual";
            _logger = logger;
        }

        public async Task<int> ObtenerPeriodoActualAsync()
        {
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

                return periodo;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<PeriodoDto>> ObtenerPeriodosDisponiblesAsync()
        {
            int actual = await ObtenerPeriodoActualAsync();
            int anterior = actual - 1;

            return new List<PeriodoDto>
            {
                new() { Id = actual,Nombre = $"Periodo {actual}", EsActual = true  },
                new() { Id = anterior,Nombre = $"Periodo {anterior}",EsActual = false }
            };
        }

        public static void InvalidarCache()
        {
            _cache = null;
            _expiry = DateTime.MinValue;
        }

        private async Task<int> LlamarSpPeriodoAsync()
        {
            try
            {
                await using var conn = new SqlConnection(_connDB);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(_spName, conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 10
                };

                var resultado = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(resultado);
            }
            catch (Exception)
            {
                return await FallbackMaxPeriodoAsync();
            }
        }

        private async Task<int> FallbackMaxPeriodoAsync()
        {
            await using var conn = new SqlConnection(_connDB);
            await conn.OpenAsync();

            const string sql = @"
                SELECT MAX(id_periodo)
                FROM dbo.DBICET
                WHERE id_periodo IS NOT NULL";

            await using var cmd = new SqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }

    public class PeriodoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool EsActual { get; set; }
    }
}
