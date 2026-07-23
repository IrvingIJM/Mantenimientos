using Microsoft.Data.SqlClient;
using Microsoft.Win32;
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

        // Rutas disponibles
        public async Task<List<int>> ObtenerRutasAsync()
        {
            var lista = new List<int>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                SELECT DISTINCT suc.RUTA FROM Iker.dbo.Sucursales suc
                INNER JOIN mttos.dbo.Seguimientos s ON s.CLV_SUC = suc.CLV_SUC
                WHERE suc.ACTIVO = 1 AND suc.RUTA IS NOT NULL
                ORDER BY suc.RUTA";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                lista.Add(Convert.ToInt32(reader.GetByte(0)));
            return lista;
        }

        // Sucursales de una ruta
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

        // informacion completa de una sucursal por CLV_SUC
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

        // carga todas las sucursales activas una sola vez
        public async Task<List<SucursalDto>> ObtenerSucursalesActivasAsync()
        {
            var lista = new List<SucursalDto>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            const string sql = @"
                SELECT CLV_SUC, Sucursal, RUTA, ID_REG
                FROM Iker.dbo.Sucursales
                WHERE ACTIVO = 1";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                lista.Add(LeerSucursalDto(reader));
            return lista;
        }

        // hay manera de mejorar la busqueda de sucursal por nombre, si es asi, impkemeta un algoritmo de busqueda mas preciso, que pueda encontrar coincidencias parciales y exactas, y que pueda manejar errores tipograficos y variaciones de nombre.
        //te mando un ejemplo de como se muestra en el excel y como se encuentra en la base de datos, para que puedas hacer una busqueda mas precisa y exacta, y que pueda encontrar coincidencias parciales y exactas, y que pueda manejar errores tipograficos y variaciones de nombre. adjunto el servicio (EmpDataService) 
        // CORRECCIONES:
        // 1. Ordenar el mensajse de suc importadas por orden alfabetico de nombre de sucursal
        // 2. el mensajse se uetre por encima de la tabla de seguimiento, y no al principio porque baja toda la tabla,
        // 3. Anadir un filtro en la tabla de sucursales para buscar por letra y que este encuentre todas las coincidencias de la letra, y que se pueda seleccionar la sucursal deseada, y que se pueda actualizar el seguimiento de esa sucursal.
        // Importación completada correctamente.
        // Registros procesados: 283
        // Registros actualizados: 281
        // Coincidencias múltiples: 2
        // Sucursales no encontradas: 0
        // Se actuaizaron 281 de 283 sucursales y hay 2 con coincidencias multiples
        // pero en la tabla principal hay 8 sucursales que aparecen como No programadas (No se le inserto ninguna fecha), poeque esta pasando esto y como se lsolucionaria


        // Búsqueda robusta de sucursal combinando coincidencias exactas, intersección y Distancia de Levenshtein (Fuzzy Matching)
        public static ResultadoBusquedaSucursal BuscarSucursalPorNombre(string nombreExcel, IReadOnlyList<SucursalDto> sucursales)
        {
            if (string.IsNullOrWhiteSpace(nombreExcel) || sucursales.Count == 0)
                return ResultadoBusquedaSucursal.NoEncontrada();

            // Normalización inicial
            string normalizadoExcel = NormalizarTexto(nombreExcel);

            // Búsqueda Exacta Rápida (sin quitar palabras clave)
            var exactas = sucursales.Where(s => NormalizarTexto(s.Nombre) == normalizadoExcel).ToList();
            if (exactas.Count == 1) return ResultadoBusquedaSucursal.Encontrada(exactas[0].CLV_SUC);
            if (exactas.Count > 1) return ResultadoBusquedaSucursal.Impreciso();

            // Quitar palabras comunes/corporativas para quedarse con la coincidencia de la sucursal
            string coincidenciaExcel = ExtraerCoincidencia(normalizadoExcel);
            if (string.IsNullOrWhiteSpace(coincidenciaExcel)) return ResultadoBusquedaSucursal.NoEncontrada();

            var candidatosCoincidencia = sucursales
                .Select(s => new { s.CLV_SUC, CoincidenciaBD = ExtraerCoincidencia(NormalizarTexto(s.Nombre)) })
                .ToList();

            // Búsqueda Exacta por Esencia
            var coincidenciaExacta = candidatosCoincidencia.Where(c => c.CoincidenciaBD == coincidenciaExcel).ToList();
            if (coincidenciaExacta.Count == 1) return ResultadoBusquedaSucursal.Encontrada(coincidenciaExacta[0].CLV_SUC);
            if (coincidenciaExacta.Count > 1) return ResultadoBusquedaSucursal.Impreciso();

            // Búsqueda Difusa (Fuzzy Matching) con Levenshtein para errores tipográficos
            double umbralAceptacion = 0.50;
            var resultadosFuzzy = candidatosCoincidencia
                .Select(c => new
                {
                    c.CLV_SUC,
                    Similitud = CalcularSimilitud(coincidenciaExcel, c.CoincidenciaBD)
                })
                .Where(x => x.Similitud >= umbralAceptacion)
                .OrderByDescending(x => x.Similitud)
                .ToList();

            if (resultadosFuzzy.Count == 0)
            {
                var palabrasExcel = coincidenciaExcel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var resultadosInterseccion = candidatosCoincidencia
                    .Where(c =>
                    {
                        var palabrasBD = c.CoincidenciaBD.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (palabrasBD.Length < palabrasExcel.Length) return false;
                        int coincidenciaExcel = palabrasExcel.Count(pe => palabrasBD.Any(pb => CalcularSimilitud(pe, pb) >= 0.80));
                        int coincidenciaBD = palabrasBD.Count(pb => palabrasExcel.Any(pe => CalcularSimilitud(pb, pe) >= 0.80));
                        return coincidenciaExcel == palabrasExcel.Length || coincidenciaBD == palabrasBD.Length;
                    })
                    .ToList();

                if (resultadosInterseccion.Count == 1) return ResultadoBusquedaSucursal.Encontrada(resultadosInterseccion[0].CLV_SUC);
                if (resultadosInterseccion.Count > 1) return ResultadoBusquedaSucursal.Impreciso();

                return ResultadoBusquedaSucursal.NoEncontrada();
            }

            // el que tiene el mayor puntaje
            if (resultadosFuzzy.Count == 1) return ResultadoBusquedaSucursal.Encontrada(resultadosFuzzy[0].CLV_SUC);

            // Si hay un empate exacto en el porcentaje superior
            if (resultadosFuzzy[0].Similitud == resultadosFuzzy[1].Similitud)
                return ResultadoBusquedaSucursal.Impreciso();

            // Retornamos el de mayor puntuación (está ordenado de forma descendente)
            return ResultadoBusquedaSucursal.Encontrada(resultadosFuzzy[0].CLV_SUC);
        }

        // metodos auxiliares para la buqueda

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            // Limpieza básica de signos y espacios
            string limpio = texto.Trim().Replace("(", " ").Replace(")", " ").Replace("-", " ").Replace(".", " ");
            limpio = System.Text.RegularExpressions.Regex.Replace(limpio, @"\s+", " ").ToLowerInvariant();

            // Quitar acentos
            var normalizadoFormD = limpio.Normalize(System.Text.NormalizationForm.FormD);
            var resultado = new System.Text.StringBuilder();

            foreach (char ch in normalizadoFormD)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                    resultado.Append(ch);
            }
            return resultado.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        private static string ExtraerCoincidencia(string textoNormalizado)
        {
            string[] palabrasIgnorar = { "intermedio", "bimbo", "ceve", "cd", "de"};

            var palabras = textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var palabrasUtiles = palabras.Where(p => !palabrasIgnorar.Contains(p));

            return string.Join(" ", palabrasUtiles);
        }

        // Implementación del Algoritmo de Distancia de Levenshtein
        private static double CalcularSimilitud(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 1.0 : 0.0;
            if (string.IsNullOrEmpty(target)) return 0.0;

            int n = source.Length;
            int m = target.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            int operaciones = d[n, m];
            int maxLongitud = Math.Max(n, m);
            return 1.0 - ((double)operaciones / maxLongitud);
        }

        // Fechas reales de DBICET
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

        // Consulta principal
        public async Task<List<SeguimientoJoinDto>> ObtenerSeguimientosAsync(
            int periodo,
            int? filtroRuta = null,
            string? filtroEmpresa = null,
            int? filtroRegion = null,
            int? filtroMesInicio = null,
            int? filtroMesFin = null,
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
            if (filtroMesInicio.HasValue && filtroMesFin.HasValue)
            {
                if (filtroMesInicio.Value <= filtroMesFin.Value)
                {
                    sql.Append(" AND dbr.F_Termino > '1900-01-01' AND MONTH(dbr.F_Termino) BETWEEN @MesIni AND @MesFin");
                }
                else
                {
                    sql.Append(" AND dbr.F_Termino > '1900-01-01' AND (MONTH(dbr.F_Termino) >= @MesIni OR MONTH(dbr.F_Termino) <= @MesFin)");
                }
                cmd.Parameters.AddWithValue("@MesIni", filtroMesInicio.Value);
                cmd.Parameters.AddWithValue("@MesFin", filtroMesFin.Value);
            }
            else if (filtroMesInicio.HasValue)
            {
                sql.Append(" AND dbr.F_Termino > '1900-01-01' AND MONTH(dbr.F_Termino) = @MesIni");
                cmd.Parameters.AddWithValue("@MesIni", filtroMesInicio.Value);
            }
            else if (filtroMesFin.HasValue)
            {
                sql.Append(" AND dbr.F_Termino > '1900-01-01' AND MONTH(dbr.F_Termino) = @MesFin");
                cmd.Parameters.AddWithValue("@MesFin", filtroMesFin.Value);
            }
            if (ocultarSinFecha)
            {
                sql.Append(" AND dbr.F_Termino IS NOT NULL AND dbr.F_Termino > '1900-01-01'");
            }

            if (filtroMesInicio.HasValue)
            {
                sql.Append(" ORDER BY (MONTH(dbr.F_Termino) - @MesIni + 12) % 12, suc.RUTA, suc.Sucursal");
            }
            else
            {
                sql.Append(" ORDER BY suc.RUTA, suc.Sucursal");
            }
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

        // Helper privado
        private static SucursalDto LeerSucursalDto(SqlDataReader r) => new()
        {
            CLV_SUC = r["CLV_SUC"].ToString()!,
            Nombre = r["Sucursal"].ToString()!,
            RUTA = r.GetByte(r.GetOrdinal("RUTA")),
            REGION = r.GetByte(r.GetOrdinal("ID_REG"))
        };

    }
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
        (FECHA_INI_RE.HasValue && FECHA_INI_ES.HasValue)
            ? (int?)(FECHA_INI_RE.Value.Date - FECHA_INI_ES.Value.Date).Days
            : null;
}

public class ExcelUpDto
{
    public int TotalFilas { get; set; }
    public int Actualizados { get; set; }
    public int NoEncontrados { get; set; }
    public int Imprecisos { get; set; }

    public List<string> NombresNoEncontrados { get; set; } = new();
    public List<string> NombresImprecisos { get; set; } = new();
    public List<string> NombresActualizados { get; set; } = new();
}

public class ResultadoBusquedaSucursal
{
    public string? ClvSuc { get; private set; }
    public bool EsImpreciso { get; private set; }
    public bool Encontrado => ClvSuc != null;

    public static ResultadoBusquedaSucursal Encontrada(string clvSuc) => new() { ClvSuc = clvSuc };
    public static ResultadoBusquedaSucursal Impreciso() => new() { EsImpreciso = true };
    public static ResultadoBusquedaSucursal NoEncontrada() => new();
}
