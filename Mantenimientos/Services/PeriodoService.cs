using Microsoft.Data.SqlClient;
using System.Data;

namespace Mantenimientos.Services
{
    public class PeriodoService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public PeriodoService(IConfiguration configuration)
        {
            _configuration = configuration;

            _connectionString =
                configuration.GetConnectionString("EmpDB")
                ?? throw new InvalidOperationException(
                    "No se encontró la cadena de conexión EmpDB.");
        }

        // Obtiene el periodo actual desde el procedimiento almacenado
        public async Task<int> ObtenerPeriodoActualAsync()
        {
            await using var conexion = new SqlConnection(_connectionString);

            await conexion.OpenAsync();

            using var cmd = new SqlCommand("PeriodoActual", conexion);

            cmd.CommandType = CommandType.StoredProcedure;

            object? resultado = await cmd.ExecuteScalarAsync();

            if (resultado == null || resultado == DBNull.Value)
                throw new Exception("No fue posible obtener el periodo actual.");

            return Convert.ToInt32(resultado);
        }

        // Devuelve el periodo anterior.
        public async Task<int> ObtenerPeriodoAnteriorAsync()
        {
            return (await ObtenerPeriodoActualAsync()) - 1;
        }

        // Devuelve nicamente los periodos que se mostraran en el filtro
        public async Task<List<int>> ObtenerPeriodosFiltroAsync()
        {
            int actual = await ObtenerPeriodoActualAsync();

            return new List<int>
            {
                actual,
                actual - 1
            };
        }
    }
}