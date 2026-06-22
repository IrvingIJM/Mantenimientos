using Mantenimientos.Models.ViewModels;
using Microsoft.Data.SqlClient;
using System.Text;

namespace Mantenimientos.Services
{
    public class EmpDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<EmpDataService> _logger;

        public EmpDataService(
            IConfiguration configuracion,
            ILogger<EmpDataService> logger)
        {
            _connectionString = configuracion.GetConnectionString("MttosConnection")
                ?? throw new InvalidOperationException(
                    "No se encontró 'MttosConnection' en appsettings.json");
            _logger = logger;
        }

        // Consultas para la vista principal
        public async Task<List<SeguimientoViewModel>> ObtenerSeguimientosAsync(
            int? filtroRuta = null,
            string? filtroRegion = null,
            int? filtroMes = null,
            int periodo = 7,
            bool soloConFecha = false)
        {
            var lista = new List<SeguimientoViewModel>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = new StringBuilder(@"
                    SELECT
                        s.ID,
                        s.CLV_SUC,
                        suc.RUTA,
                        suc.Sucursal AS NOMBRE_SUCURSAL,
                        CAST(suc.ID_REG AS NVARCHAR(50)) AS REGION,
                        s.FECHA_INI_ES,
                        s.FECHA_FIN_ES,
                        CASE WHEN db.F_Inicio  IS NULL OR db.F_Inicio <= '1900-01-01'
                             THEN NULL ELSE db.F_Inicio  END AS FECHA_INI_RE,
                        CASE WHEN db.F_Termino IS NULL OR db.F_Termino <= '1900-01-01'
                             THEN NULL ELSE db.F_Termino END AS FECHA_FIN_RE,
                        s.DIAS_ATRASO,
                        s.OBSERVACIONES
                    FROM mttos.dbo.Seguimientos s
                    INNER JOIN Iker.dbo.Sucursales suc
                           ON  s.CLV_SUC = suc.CLV_SUC
                    LEFT  JOIN (
                        SELECT CLV_SUC, F_Inicio, F_Termino,
                               ROW_NUMBER() OVER (PARTITION BY CLV_SUC
                                                  ORDER BY F_Inicio DESC) AS fila
                        FROM   Iker.dbo.DBICET
                        WHERE  id_periodo = @Periodo
                    ) db ON s.CLV_SUC = db.CLV_SUC AND db.fila = 1
                    WHERE suc.ACTIVO = 1
                ");

                var parameters = new List<SqlParameter>
                {
                    new("@Periodo", periodo)
                };

                if (filtroRuta.HasValue)
                {
                    sql.Append(" AND suc.RUTA = @Ruta");
                    parameters.Add(new("@Ruta", filtroRuta.Value));
                }

                if (!string.IsNullOrWhiteSpace(filtroRegion))
                {
                    sql.Append(" AND CAST(suc.ID_REG AS NVARCHAR(50)) = @Region");
                    parameters.Add(new("@Region", filtroRegion));
                }

                if (filtroMes.HasValue)
                {
                    sql.Append(" AND MONTH(s.FECHA_INI_ES) = @Mes");
                    parameters.Add(new("@Mes", filtroMes.Value));
                }

                //// "Si hay un registro que no tenga fecha, no debe aparecer nada"
                //if (soloConFecha)
                //{
                //    sql.Append(@" AND db.F_Inicio IS NOT NULL
                //                  AND db.F_Inicio > '1900-01-01'");
                //}

                sql.Append(" ORDER BY suc.RUTA, suc.Sucursal");

                await using var cmd = new SqlCommand(sql.ToString(), conn);
                cmd.Parameters.AddRange(parameters.ToArray());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new SeguimientoViewModel
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        CLV_SUC = reader.GetInt32(reader.GetOrdinal("CLV_SUC")),
                        RUTA = reader.GetInt32(reader.GetOrdinal("RUTA")),
                        SUCURSAL = reader.GetString(reader.GetOrdinal("NOMBRE_SUCURSAL")),
                        REGION = reader["REGION"]?.ToString() ?? string.Empty,
                        FECHA_INI_ES = reader["FECHA_INI_ES"] as DateTime?,
                        FECHA_FIN_ES = reader["FECHA_FIN_ES"] as DateTime?,
                        FECHA_INI_RE = reader["FECHA_INI_RE"] as DateTime?,
                        FECHA_FIN_RE = reader["FECHA_FIN_RE"] as DateTime?,
                        DIAS_ATRASO = reader["DIAS_ATRASO"] as int? ?? 0,
                        OBSERVACIONES = reader["OBSERVACIONES"] as string
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener seguimientos.");
                throw;
            }
            return lista;
        }

        public async Task<ObservacionVM?> ObtenerDatosObservacionAsync(int id, int periodo = 7)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT
                        s.ID,
                        s.CLV_SUC,
                        suc.RUTA,
                        suc.Sucursal AS NOMBRE_SUCURSAL,
                        CAST(suc.ID_REG AS NVARCHAR(50)) AS REGION,
                        s.FECHA_INI_ES,
                        s.FECHA_FIN_ES,
                        CASE WHEN db.F_Inicio  IS NULL OR db.F_Inicio <= '1900-01-01'
                             THEN NULL ELSE db.F_Inicio END AS FECHA_INI_RE,
                        CASE WHEN db.F_Termino IS NULL OR db.F_Termino <= '1900-01-01'
                             THEN NULL ELSE db.F_Termino END AS FECHA_FIN_RE,
                        s.DIAS_ATRASO,
                        s.OBSERVACIONES
                    FROM mttos.dbo.Seguimientos s
                    INNER JOIN Iker.dbo.Sucursales suc
                           ON  s.CLV_SUC = suc.CLV_SUC
                    LEFT  JOIN (
                        SELECT CLV_SUC, F_Inicio, F_Termino,
                               ROW_NUMBER() OVER (PARTITION BY CLV_SUC
                                                  ORDER BY F_Inicio DESC) AS fila
                        FROM   Iker.dbo.DBICET
                        WHERE  id_periodo = @Periodo
                    ) db ON s.CLV_SUC = db.CLV_SUC AND db.fila = 1
                    WHERE s.ID = @Id";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Periodo", periodo);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ObservacionVM
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        CLV_SUC = reader.GetInt32(reader.GetOrdinal("CLV_SUC")),
                        RUTA = reader.GetInt32(reader.GetOrdinal("RUTA")),
                        SUCURSAL = reader.GetString(reader.GetOrdinal("NOMBRE_SUCURSAL")),
                        REGION = reader["REGION"]?.ToString() ?? string.Empty,
                        FECHA_INI_ES = reader["FECHA_INI_ES"] as DateTime?,
                        FECHA_FIN_ES = reader["FECHA_FIN_ES"] as DateTime?,
                        FECHA_INI_RE = reader["FECHA_INI_RE"] as DateTime?,
                        FECHA_FIN_RE = reader["FECHA_FIN_RE"] as DateTime?,
                        DIAS_ATRASO = reader["DIAS_ATRASO"] as int?,
                        OBSERVACIONES = reader["OBSERVACIONES"] as string
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos de observación para ID {Id}.", id);
            }
            return null;
        }

        public async Task SincronizarDiasAtrasoAsync(int periodo = 7)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"
                    UPDATE s
                    SET s.DIAS_ATRASO =
                        CASE
                            WHEN s.FECHA_INI_ES IS NULL THEN NULL
                            WHEN db.F_Inicio IS NULL OR db.F_Inicio <= '1900-01-01' THEN NULL
                            ELSE DATEDIFF(day, s.FECHA_INI_ES, db.F_Inicio)
                        END
                    FROM mttos.dbo.Seguimientos s
                    INNER JOIN (
                        SELECT CLV_SUC, F_Inicio,
                               ROW_NUMBER() OVER (PARTITION BY CLV_SUC
                                                  ORDER BY F_Inicio DESC) AS fila
                        FROM   Iker.dbo.DBICET
                        WHERE  id_periodo = @Periodo
                    ) db ON s.CLV_SUC = db.CLV_SUC AND db.fila = 1";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Periodo", periodo);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar días de atraso.");
                throw;
            }
        }

        //Listas para filtros
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
                    INNER JOIN mttos.dbo.Seguimientos s ON suc.CLV_SUC = s.CLV_SUC
                    WHERE suc.ACTIVO = 1 AND suc.RUTA IS NOT NULL
                    ORDER BY suc.RUTA";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(reader.GetInt32(0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rutas.");
                throw;
            }
            return lista;
        }

        // Regonesactivas con registrp
        public async Task<List<string>> ObtenerRegionesAsync()
        {
            var lista = new List<string>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT CAST(suc.ID_REG AS NVARCHAR(50)) AS REGION
                    FROM   Iker.dbo.Sucursales suc
                    INNER JOIN mttos.dbo.Seguimientos s ON suc.CLV_SUC = s.CLV_SUC
                    WHERE  suc.ACTIVO = 1 AND suc.ID_REG IS NOT NULL
                    ORDER  BY REGION";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener regiones.");
            }
            return lista;
        }

        // Periodos disponibles en DBICET
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
                    ORDER  BY id_periodo";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(reader.GetInt32(0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener periodos.");
            }
            return lista;
        }

        //Sucursales activas de una ruta
        public async Task<List<(int ClvSuc, string Nombre)>> ObtenerSucursalesPorRutaAsync(int ruta)
        {
            var lista = new List<(int, string)>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT suc.CLV_SUC, suc.Sucursal
                    FROM   Iker.dbo.Sucursales suc
                    INNER JOIN mttos.dbo.Seguimientos s ON suc.CLV_SUC = s.CLV_SUC
                    WHERE  suc.ACTIVO = 1 AND suc.RUTA = @Ruta
                    ORDER  BY suc.Sucursal";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Ruta", ruta);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add((reader.GetInt32(0), reader.GetString(1)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener sucursales para ruta {Ruta}.", ruta);
            }
            return lista;
        }

        // Todas las sucursales activas con registro en Seguimientos
        public async Task<List<(int ClvSuc, string Nombre)>> ObtenerTodasSucursalesAsync()
        {
            var lista = new List<(int, string)>();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT DISTINCT suc.CLV_SUC, suc.Sucursal
                    FROM   Iker.dbo.Sucursales suc
                    INNER JOIN mttos.dbo.Seguimientos s ON suc.CLV_SUC = s.CLV_SUC
                    WHERE  suc.ACTIVO = 1
                    ORDER  BY suc.Sucursal";
                await using var cmd = new SqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add((reader.GetInt32(0), reader.GetString(1)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todas las sucursales.");
            }
            return lista;
        }
    }
}