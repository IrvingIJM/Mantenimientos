using Microsoft.Data.SqlClient;
using System.Text;

namespace Mantenimientos.Services
{
    public class EmpDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<EmpDataService> _logger;

        private static readonly DateTime FechaDefault = new(1900, 1, 1);

        public EmpDataService(IConfiguration configuracion, ILogger<EmpDataService> logger)
        {
            _connectionString = configuracion.GetConnectionString("MttosConnection")
                ?? throw new InvalidOperationException(
                    "No se encontró 'MttosConnection' en appsettings.json");
            _logger = logger;
        }

        // Obtenemos rutas y sucursales activas para el filtro dinámico del Index.
        public async Task<List<int>> ObtenerRutasAsync()
        {
            var lista = new List<int>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT suc.RUTA
                    FROM Iker.dbo.Sucursales suc
                    INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
                    WHERE suc.ACTIVO = 1
                      AND suc.RUTA IS NOT NULL
                    ORDER BY suc.RUTA";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(Convert.ToInt32(reader.GetByte(0)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rutas.");
            }
            return lista;
        }

        // Sucursales activas de una ruta
        public async Task<List<SucursalDto>> ObtenerSucursalesPorRutaAsync(int ruta)
        {
            var lista = new List<SucursalDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT suc.CLV_SUC, suc.Sucursal, suc.RUTA, suc.ID_REG
                    FROM Iker.dbo.Sucursales suc
                    INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
                    WHERE suc.ACTIVO = 1
                      AND suc.RUTA = @Ruta
                    ORDER BY suc.Sucursal";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Ruta", ruta);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(LeerSucursalDto(reader));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener sucursales para ruta {Ruta}.", ruta);
            }
            return lista;
        }

        //Información completa de una sucursal
        public async Task<SucursalDto?> ObtenerInfoSucursalAsync(string clvSuc)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT TOP 1 CLV_SUC, Sucursal, RUTA, ID_REG
                    FROM Iker.dbo.Sucursales
                    WHERE CLV_SUC = @ClvSuc";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ClvSuc", clvSuc);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return LeerSucursalDto(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener info de sucursal {ClvSuc}.", clvSuc);
            }
            return null;
        }

        // Fechas reales (F_Inicio, F_Termino) de una sucursal para el periodo indicado.
        public async Task<FechasRealesDto?> ObtenerFechasRealesAsync(string clvSuc, int periodo)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT TOP 1 F_Inicio, F_Termino
                    FROM Iker.dbo.DBICET
                    WHERE CLV_SUC = @ClvSuc
                      AND id_periodo = @Periodo
                    ORDER BY F_Inicio DESC";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ClvSuc", clvSuc);
                cmd.Parameters.AddWithValue("@Periodo", periodo);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var fi = reader["F_Inicio"] as DateTime?;
                    var ft = reader["F_Termino"] as DateTime?;
                    return new FechasRealesDto
                    {
                        FechaInicio = (fi.HasValue && fi.Value > FechaDefault) ? fi : null,
                        FechaFin = (ft.HasValue && ft.Value > FechaDefault) ? ft : null
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al obtener fechas reales para {ClvSuc}, periodo {Periodo}.",
                    clvSuc, periodo);
            }
            return null;
        }

        public async Task<List<SeguimientoJoinDto>> ObtenerSeguimientosAsync(
            int periodo,
            int? filtroRuta = null,
            int? filtroRegion = null,
            int? filtroMes = null,
            bool ocultarSinFecha = false)
        {
            var lista = new List<SeguimientoJoinDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = new StringBuilder(@"
                    SELECT
                        s.ID,
                        s.CLV_SUC,
                        s.ID_PERIODO,
                        suc.Sucursal AS SUCURSAL,
                        suc.RUTA,
                        suc.ID_REG AS REGION,
                        s.FECHA_INI_ES,
                        s.FECHA_FIN_ES,
                        dbr.F_Inicio AS FECHA_INI_RE,
                        dbr.F_Termino AS FECHA_FIN_RE,
                        s.OBSERVACIONES
                    FROM mttos.dbo.Seguimientos s
                    INNER JOIN Iker.dbo.Sucursales suc
                           ON s.CLV_SUC = suc.CLV_SUC
                    LEFT JOIN (
                        SELECT CLV_SUC, F_Inicio, F_Termino,
                               ROW_NUMBER() OVER (
                                   PARTITION BY CLV_SUC
                                   ORDER BY F_Inicio DESC
                               ) AS fila
                        FROM Iker.dbo.DBICET
                        WHERE id_periodo = @Periodo
                    ) dbr ON s.CLV_SUC = dbr.CLV_SUC AND dbr.fila = 1
                    WHERE suc.ACTIVO   = 1
                      AND s.ID_PERIODO = @Periodo");

                // Parámetros 
                await using var cmd = new SqlCommand();
                cmd.Connection = conn;
                cmd.Parameters.AddWithValue("@Periodo", periodo);

                if (filtroRuta.HasValue)
                {
                    sql.Append(" AND suc.RUTA = @Ruta");
                    cmd.Parameters.AddWithValue("@Ruta", filtroRuta.Value);
                }
                if (filtroRegion.HasValue)
                {
                    sql.Append(" AND suc.ID_REG = @Region");
                    cmd.Parameters.AddWithValue("@Region", filtroRegion.Value);
                }
                if (filtroMes.HasValue)
                {
                    sql.Append(@" AND dbr.F_Inicio > '1900-01-01'
                      AND MONTH(dbr.F_Inicio) = @Mes");
                    cmd.Parameters.AddWithValue("@Mes", filtroMes.Value);
                }
                if (ocultarSinFecha)
                {
                    sql.Append(@"
                      AND dbr.F_Inicio IS NOT NULL
                      AND dbr.F_Inicio > '1900-01-01'");
                }

                sql.Append(" ORDER BY suc.RUTA, suc.Sucursal");
                cmd.CommandText = sql.ToString();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fi = reader["FECHA_INI_RE"] as DateTime?;
                    var ft = reader["FECHA_FIN_RE"] as DateTime?;
                    lista.Add(new SeguimientoJoinDto
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        CLV_SUC = reader["CLV_SUC"].ToString()!,
                        ID_PERIODO = reader.GetInt32(reader.GetOrdinal("ID_PERIODO")),
                        SUCURSAL = reader["SUCURSAL"].ToString()!,
                        RUTA = reader.GetByte(reader.GetOrdinal("RUTA")),
                        REGION = reader.GetByte(reader.GetOrdinal("REGION")),
                        FECHA_INI_ES = reader["FECHA_INI_ES"] as DateTime?,
                        FECHA_FIN_ES = reader["FECHA_FIN_ES"] as DateTime?,
                        FECHA_INI_RE = (fi.HasValue && fi.Value > FechaDefault) ? fi : null,
                        FECHA_FIN_RE = (ft.HasValue && ft.Value > FechaDefault) ? ft : null,
                        OBSERVACIONES = reader["OBSERVACIONES"] as string
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener seguimientos con JOIN (periodo {P}).", periodo);
                throw;
            }
            return lista;
        }

        // Helpers privados
        private static SucursalDto LeerSucursalDto(SqlDataReader r) => new()
        {
            CLV_SUC = r["CLV_SUC"].ToString()!,
            Nombre = r["Sucursal"].ToString()!,
            RUTA = r.GetByte(r.GetOrdinal("RUTA")),
            REGION = r.GetByte(r.GetOrdinal("ID_REG"))
        };
    }

    public class SucursalDto
    {
        public string CLV_SUC { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public byte RUTA { get; set; }
        public byte REGION { get; set; }
    }

    public class FechasRealesDto
    {
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
    }

    public class SeguimientoJoinDto
    {
        public int ID { get; set; }
        public string CLV_SUC { get; set; } = string.Empty;
        public int ID_PERIODO { get; set; }
        public string SUCURSAL { get; set; } = string.Empty;
        public byte RUTA { get; set; }
        public byte REGION { get; set; }
        public DateTime? FECHA_INI_ES { get; set; }
        public DateTime? FECHA_FIN_ES { get; set; }
        public DateTime? FECHA_INI_RE { get; set; }
        public DateTime? FECHA_FIN_RE { get; set; }
        public string? OBSERVACIONES { get; set; }
        public int? Dias =>
       (FECHA_FIN_RE.HasValue && FECHA_FIN_ES.HasValue)
           ? (int?)(FECHA_FIN_RE.Value.Date - FECHA_FIN_ES.Value.Date).Days
           : null;
    }
}
