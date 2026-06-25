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

        // Rutas que tienen al menos un Seguimiento activo
        public async Task<List<int>> ObtenerRutasAsync()
        {
            var lista = new List<int>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT suc.RUTA
                    FROM   Iker.dbo.Sucursales   suc
                    INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
                    WHERE  suc.ACTIVO = 1
                      AND  suc.RUTA  IS NOT NULL
                    ORDER BY suc.RUTA";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(Convert.ToInt32(reader.GetByte(0)));
            }
            catch (Exception ex) { _logger.LogError(ex, "Error al obtener rutas."); }
            return lista;
        }

        // Regiones que tienen al menos un Seguimiento activo
        public async Task<List<int>> ObtenerRegionesAsync()
        {
            var lista = new List<int>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"
            SELECT DISTINCT suc.ID_REG
            FROM   Iker.dbo.Sucursales   suc
            INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
            WHERE  suc.ACTIVO = 1
              AND  suc.ID_REG IS NOT NULL
            ORDER BY suc.ID_REG";

                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        lista.Add(Convert.ToInt32(reader.GetByte(0)));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener regiones.");
            }
            return lista;
        }

        // Periodos disponibles en DBICET, ordenados descendente
        public async Task<List<int>> ObtenerPeriodosAsync()
        {
            var lista = new List<int>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"
            SELECT DISTINCT id_periodo
            FROM   Iker.dbo.DBICET
            WHERE  id_periodo IS NOT NULL
            ORDER BY id_periodo DESC";

                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        lista.Add(reader.GetByte(0));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener periodos.");
            }
            return lista;
        }

        // Sucursales activas de una ruta para el filtro dinámico del Index
        public async Task<List<SucursalDto>> ObtenerSucursalesPorRutaAsync(int ruta)
        {
            var lista = new List<SucursalDto>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT suc.CLV_SUC, suc.Sucursal, suc.RUTA, suc.ID_REG
                    FROM   Iker.dbo.Sucursales suc
                    INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
                    WHERE  suc.ACTIVO = 1
                      AND  suc.RUTA  = @Ruta
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

        // Información completa de una sucursal por CLV_SUC
        public async Task<SucursalDto?> ObtenerInfoSucursalAsync(string clvSuc)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT TOP 1 CLV_SUC, Sucursal, RUTA, ID_REG
                    FROM   Iker.dbo.Sucursales
                    WHERE  CLV_SUC = @ClvSuc";
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

        // Fechas reales del periodo 7 (el más reciente).
        public async Task<FechasRealesDto?> ObtenerFechasRealesAsync(string clvSuc, int periodo = 7)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT TOP 1 F_Inicio, F_Termino
                    FROM   Iker.dbo.DBICET
                    WHERE  CLV_SUC   = @ClvSuc
                      AND  id_periodo = @Periodo
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

        // ── Consulta principal con joins
        public async Task<List<SeguimientoJoinDto>> ObtenerSeguimientosAsync(
            int? filtroRuta = null,
            int? filtroRegion = null,
            int? filtroMes = null,
            bool ocultarSinFecha = false,
            int periodo = 7)
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
                        suc.Sucursal        AS NOMBRE_SUCURSAL,
                        suc.RUTA,
                        suc.ID_REG          AS REGION,
                        s.FECHA_INI_ES,
                        s.FECHA_FIN_ES,
                        dbr.F_Inicio        AS FECHA_INI_RE,
                        dbr.F_Termino       AS FECHA_FIN_RE,
                        s.DIAS_ATRASO,
                        s.OBSERVACIONES
                    FROM  mttos.dbo.Seguimientos   s
                    INNER JOIN Iker.dbo.Sucursales suc
                           ON  s.CLV_SUC = suc.CLV_SUC
                    LEFT  JOIN (
                        SELECT CLV_SUC, F_Inicio, F_Termino,
                               ROW_NUMBER() OVER (
                                   PARTITION BY CLV_SUC
                                   ORDER BY F_Inicio DESC
                               ) AS fila
                        FROM   Iker.dbo.DBICET
                        WHERE  id_periodo = @Periodo
                    ) dbr ON s.CLV_SUC = dbr.CLV_SUC AND dbr.fila = 1
                    WHERE  suc.ACTIVO = 1");

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
                    sql.Append(" AND MONTH(s.FECHA_INI_ES) = @Mes");
                    cmd.Parameters.AddWithValue("@Mes", filtroMes.Value);
                }
                if (ocultarSinFecha)
                {
                    sql.Append(@" AND dbr.F_Inicio IS NOT NULL
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
                        SUCURSAL = reader["NOMBRE_SUCURSAL"].ToString()!,
                        RUTA = reader.GetByte(reader.GetOrdinal("RUTA")),
                        REGION = reader.GetByte(reader.GetOrdinal("REGION")),
                        FECHA_INI_ES = reader["FECHA_INI_ES"] as DateTime?,
                        FECHA_FIN_ES = reader["FECHA_FIN_ES"] as DateTime?,
                        FECHA_INI_RE = (fi.HasValue && fi.Value > FechaDefault) ? fi : null,
                        FECHA_FIN_RE = (ft.HasValue && ft.Value > FechaDefault) ? ft : null,
                        DIAS_ATRASO = reader["DIAS_ATRASO"] as int?,
                        OBSERVACIONES = reader["OBSERVACIONES"] as string
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener seguimientos con JOIN.");
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

    // DTOs
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
        public string SUCURSAL { get; set; } = string.Empty;
        public byte RUTA { get; set; }
        public byte REGION { get; set; }
        public DateTime? FECHA_INI_ES { get; set; }
        public DateTime? FECHA_FIN_ES { get; set; }
        public DateTime? FECHA_INI_RE { get; set; }
        public DateTime? FECHA_FIN_RE { get; set; }
        public int? DIAS_ATRASO { get; set; }
        public string? OBSERVACIONES { get; set; }
    }
}
