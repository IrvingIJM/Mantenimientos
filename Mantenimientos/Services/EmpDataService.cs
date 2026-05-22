using Microsoft.Data.SqlClient;

namespace Mantenimientos.Services
{
    public class EmpDataService
    {
        private readonly string? _connectionString;
        private readonly ILogger<EmpDataService> _logger;

        private static readonly DateTime FechaDefault = new(1900, 1, 1);
        public EmpDataService(
            IConfiguration configuracion,
            ILogger<EmpDataService> logger)
        {
            _connectionString = configuracion.GetConnectionString("DatabaseConnection") ?? throw new InvalidOperationException(
                "No se encontro 'DatabaseConnection' en appsettings.json");
            _logger = logger;
        }

    }
}
