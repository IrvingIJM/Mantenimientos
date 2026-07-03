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
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            _connectionString = configuracion.GetConnectionString("MttosConnection")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'MttosConnection'.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ── Rutas disponibles ─────────────────────────────────────────────────
        public async Task<List<int>> ObtenerRutasAsync()
        {
            var lista = new List<int>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                SELECT DISTINCT suc.RUTA
                FROM Iker.dbo.Sucursales suc
                INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
                WHERE suc.ACTIVO = 1 AND suc.RUTA IS NOT NULL
                ORDER BY suc.RUTA";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                lista.Add(Convert.ToInt32(reader.GetByte(0)));
            return lista;
        }

        // ── Sucursales de una ruta ────────────────────────────────────────────
        public async Task<List<SucursalDto>> ObtenerSucursalesPorRutaAsync(int ruta)
        {
            var lista = new List<SucursalDto>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                SELECT DISTINCT suc.CLV_SUC, suc.Sucursal, suc.RUTA, suc.ID_REG
                FROM Iker.dbo.Sucursales suc
                INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
                WHERE suc.ACTIVO = 1 AND suc.RUTA = @Ruta
                ORDER BY suc.Sucursal";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Ruta", ruta);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                lista.Add(LeerSucursalDto(reader));
            return lista;
        }

        // ── Info completa de una sucursal por CLV_SUC ────────────────────────
        public async Task<SucursalDto?> ObtenerInfoSucursalAsync(string clvSuc)
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
            return null;
        }

        // ── Buscar CLV_SUC por el campo Nombre ─────
        public async Task<string?> BuscarClvSucPorNombreAsync(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return null;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            nombre = nombre.Trim();

            // 1️⃣ Primero intenta coincidencia exacta (ignorando acentos)
            const string sqlExacto = @"
                    SELECT TOP 1 CLV_SUC
                    FROM Iker.dbo.Sucursales
                    WHERE LTRIM(RTRIM(Nombre)) COLLATE LATIN1_GENERAL_CI_AI = LTRIM(RTRIM(@Nombre)) COLLATE LATIN1_GENERAL_CI_AI
                    AND ACTIVO = 1";

            await using var cmd = new SqlCommand(sqlExacto, conn);
            cmd.Parameters.AddWithValue("@Nombre", nombre);
            var resultado = await cmd.ExecuteScalarAsync();

            if (resultado != null && resultado != DBNull.Value)
            {
                _logger.LogInformation($"✓ Encontrado (coincidencia exacta): {nombre} -> CLV_SUC={resultado}");
                return resultado.ToString();
            }

            // Divide el nombre en palabras clave
            var palabras = nombre.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length >= 6)
                .ToList();

            if (palabras.Count == 0)
                return null;

            // Si hay MÚLTIPLES palabras, busca sucursales que contengan TODAS (o la mayoría)
            if (palabras.Count >= 6)
            {
                // Construir una query que busque sucursales con TODAS las palabras
                var sqlTodasPalabras = @"
                        SELECT TOP 1 CLV_SUC
                        FROM Iker.dbo.Sucursales
                        WHERE ACTIVO = 1 
                        AND ";

                // Agregar condiciones para CADA palabra
                var condiciones = new List<string>();
                for (int i = 0; i < palabras.Count; i++)
                {
                    condiciones.Add($"Nombre COLLATE LATIN1_GENERAL_CI_AI LIKE CONCAT('%', @Palabra{i} COLLATE LATIN1_GENERAL_CI_AI, '%')");
                }
                sqlTodasPalabras += string.Join(" AND ", condiciones) + " ORDER BY LEN(Nombre)";

                await using var cmdMultiple = new SqlCommand(sqlTodasPalabras, conn);
                for (int i = 0; i < palabras.Count; i++)
                {
                    cmdMultiple.Parameters.AddWithValue($"@Palabra{i}", palabras[i]);
                }

                var resultadoMultiple = await cmdMultiple.ExecuteScalarAsync();
                if (resultadoMultiple != null && resultadoMultiple != DBNull.Value)
                {
                    _logger.LogInformation($"✓ Encontrado (coincide con TODAS las palabras {string.Join(", ", palabras)}): {nombre} -> CLV_SUC={resultadoMultiple}");
                    return resultadoMultiple.ToString();
                }
            }

            // Si no encuentra con todas las palabras, intenta con palabras individuales (pero prioritariamente)
            // Ordenadas por longitud de palabra (palabras más largas = más específicas primero)
            var palabrasOrdenadas = palabras.OrderByDescending(p => p.Length).ToList();

            foreach (var palabra in palabrasOrdenadas)
            {
                const string sqlPalabra = @"
                        SELECT TOP 1 CLV_SUC
                        FROM Iker.dbo.Sucursales
                        WHERE Nombre COLLATE LATIN1_GENERAL_CI_AI LIKE CONCAT('%', @Palabra COLLATE LATIN1_GENERAL_CI_AI, '%')
                        AND ACTIVO = 1
                        ORDER BY LEN(Nombre)";

                await using var cmdPalabra = new SqlCommand(sqlPalabra, conn);
                cmdPalabra.Parameters.AddWithValue("@Palabra", palabra);
                var resultadoPalabra = await cmdPalabra.ExecuteScalarAsync();

                if (resultadoPalabra != null && resultadoPalabra != DBNull.Value)
                {
                    _logger.LogInformation($"✓ Encontrado (por palabra clave '{palabra}'): {nombre} -> CLV_SUC={resultadoPalabra}");
                    return resultadoPalabra.ToString();
                }
            }

            // 5️⃣ Última opción: búsqueda parcial del nombre completo
            const string sqlLike = @"
                    SELECT TOP 1 CLV_SUC
                    FROM Iker.dbo.Sucursales
                    WHERE Nombre COLLATE LATIN1_GENERAL_CI_AI LIKE CONCAT('%', @NombreLike COLLATE LATIN1_GENERAL_CI_AI, '%')
                    AND ACTIVO = 1
                    ORDER BY LEN(Nombre)";

            await using var cmd2 = new SqlCommand(sqlLike, conn);
            cmd2.Parameters.AddWithValue("@NombreLike", nombre);
            var resultado2 = await cmd2.ExecuteScalarAsync();

            if (resultado2 != null && resultado2 != DBNull.Value)
            {
                _logger.LogInformation($"✓ Encontrado (búsqueda parcial): {nombre} -> CLV_SUC={resultado2}");
                return resultado2.ToString();
            }

            _logger.LogWarning($"❌ Sucursal no encontrada después de 5 intentos: '{nombre}'");
            return null;
        }

        // ── Fechas reales de DBICET ───────────────────────────────────────────
        public async Task<FechasRealesDto?> ObtenerFechasRealesAsync(string clvSuc, int periodo)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                SELECT TOP 1 F_Inicio, F_Termino
                FROM Iker.dbo.DBICET
                WHERE CLV_SUC = @ClvSuc AND id_periodo = @Periodo
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
            return null;
        }

        // ── Consulta principal (JOIN) ─────────────────────────────────────────
        public async Task<List<SeguimientoJoinDto>> ObtenerSeguimientosAsync(
            int periodo,
            int? filtroRuta = null,
            string? filtroEmpresa = null,
            int? filtroRegion = null,
            int? filtroMes = null,
            bool ocultarSinFecha = false)
        {
            var lista = new List<SeguimientoJoinDto>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = new StringBuilder(@"
                SELECT s.ID, s.CLV_SUC, s.ID_PERIODO,
                       suc.Sucursal AS SUCURSAL, suc.RUTA, suc.ID_REG AS REGION,
                       s.FECHA_INI_ES, s.FECHA_FIN_ES,
                       dbr.F_Inicio  AS FECHA_INI_RE,
                       dbr.F_Termino AS FECHA_FIN_RE,
                       s.OBSERVACIONES
                FROM mttos.dbo.Seguimientos s
                INNER JOIN Iker.dbo.Sucursales suc ON s.CLV_SUC = suc.CLV_SUC
                LEFT JOIN (SELECT CLV_SUC, F_Inicio, F_Termino, ROW_NUMBER() OVER (PARTITION BY CLV_SUC ORDER BY F_Inicio DESC) AS fila
                FROM Iker.dbo.DBICET WHERE id_periodo = @Periodo) dbr ON s.CLV_SUC = dbr.CLV_SUC AND dbr.fila = 1
                WHERE suc.ACTIVO = 1 AND s.ID_PERIODO = @Periodo");

            await using var cmd = new SqlCommand();
            cmd.Connection = conn;
            cmd.Parameters.AddWithValue("@Periodo", periodo);

            if (filtroRuta.HasValue)
            {
                sql.Append(" AND suc.RUTA = @Ruta");
                cmd.Parameters.AddWithValue("@Ruta", filtroRuta.Value);
            }
            if (!string.IsNullOrEmpty(filtroEmpresa))
            {
                sql.Append(" AND suc.CLV_SUC LIKE @Empresa");
                cmd.Parameters.AddWithValue("@Empresa", filtroEmpresa + "%");
            }
            if (filtroRegion.HasValue)
            {
                sql.Append(" AND suc.ID_REG = @Region");
                cmd.Parameters.AddWithValue("@Region", filtroRegion.Value);
            }
            if (filtroMes.HasValue)
            {
                sql.Append(" AND dbr.F_Inicio > '1900-01-01' AND MONTH(dbr.F_Inicio) = @Mes");
                cmd.Parameters.AddWithValue("@Mes", filtroMes.Value);
            }
            if (ocultarSinFecha)
            {
                sql.Append(" AND dbr.F_Inicio IS NOT NULL AND dbr.F_Inicio > '1900-01-01'");
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
            return lista;
        }

        // ── Helper privado ────────────────────────────────────────────────────
        private static SucursalDto LeerSucursalDto(SqlDataReader r) => new()
        {
            CLV_SUC = r["CLV_SUC"].ToString()!,
            Nombre = r["Sucursal"].ToString()!,
            RUTA = r.GetByte(r.GetOrdinal("RUTA")),
            REGION = r.GetByte(r.GetOrdinal("ID_REG"))
        };

        // ─── Normalizar texto: remover acentos y convertir a minúsculas ───────────────
        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            // Remover acentos: descompone el texto y elimina combinaciones diacríticas
            var normalizadoFormD = texto.Normalize(System.Text.NormalizationForm.FormD);
            var resultado = new System.Text.StringBuilder();

            foreach (char ch in normalizadoFormD)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                    resultado.Append(ch);
            }

            // Convertir a minúsculas
            return resultado.ToString()
                .Normalize(System.Text.NormalizationForm.FormC)
                .ToLowerInvariant();
        }
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────
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

public class ExcelUpDto
{
    public int TotalFilas { get; set; }
    public int Actualizados { get; set; }
    public int NoEncontrados { get; set; }
    public List<string> NombresNoEncontrados { get; set; } = new();
}