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

        // busca una sucursal por nombre, con alta precisión usando extracción de "esencia" e intersección de palabras clave
        public static ResultadoBusquedaSucursal BuscarSucursalPorNombre(string nombreExcel, IReadOnlyList<SucursalDto> sucursales)
        {
            if (string.IsNullOrWhiteSpace(nombreExcel) || sucursales.Count == 0)
                return ResultadoBusquedaSucursal.NoEncontrada();

            // normalizacion 
            string original = nombreExcel.Trim();
            original = original.Replace("Sucusal", "Sucursal", StringComparison.OrdinalIgnoreCase);
            original = original.Replace("(", " ").Replace(")", " ").Replace("-", " ");

            string sinDobleEspacio = ColapsarEspacios(original);
            string normalizadoExcel = QuitarAcentos(sinDobleEspacio).ToLowerInvariant();

            // coincidencia exacta
            var exactas = sucursales.Where(s => QuitarAcentos(ColapsarEspacios(s.Nombre)).ToLowerInvariant() == normalizadoExcel).ToList();
            if (exactas.Count == 1) return ResultadoBusquedaSucursal.Encontrada(exactas[0].CLV_SUC);
            if (exactas.Count > 1) return ResultadoBusquedaSucursal.Impreciso();

            // ignorar palablas para comparar solamntee el nombre
            string[] palabrasIgnorar = { "intermedio", "bimbo", "ceve", "." };

            string ObtenerCondicion(string texto)
            {
                var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var palabrasUtiles = palabras.Where(p => !palabrasIgnorar.Contains(p)).OrderBy(p => p);
                return string.Join(" ", palabrasUtiles);
            }

            string condicionExcel = ObtenerCondicion(normalizadoExcel);

            if (!string.IsNullOrWhiteSpace(condicionExcel))
            {
                // busqueda por condicion exacta
                var candidatos = sucursales
                    .Select(s => new { s.CLV_SUC, Esencia = ObtenerCondicion(QuitarAcentos(ColapsarEspacios(s.Nombre)).ToLowerInvariant()) })
                    .Where(s => s.Esencia == condicionExcel)
                    .ToList();

                if (candidatos.Count == 1) return ResultadoBusquedaSucursal.Encontrada(candidatos[0].CLV_SUC);
                if (candidatos.Count > 1) return ResultadoBusquedaSucursal.Impreciso();

                var palabrasExcel = condicionExcel.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

                var candidatasSimilares = sucursales
                    .Select(s => new
                    {
                        s.CLV_SUC,
                        PalabrasBD = ObtenerCondicion(QuitarAcentos(ColapsarEspacios(s.Nombre)).ToLowerInvariant()).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                    })
                    .Where(s => s.PalabrasBD.Any() && palabrasExcel.Intersect(s.PalabrasBD).Count() == palabrasExcel.Count)
                    .ToList();

                if (candidatasSimilares.Count == 1) return ResultadoBusquedaSucursal.Encontrada(candidatasSimilares[0].CLV_SUC);
                if (candidatasSimilares.Count > 1) return ResultadoBusquedaSucursal.Impreciso();
            }

            return ResultadoBusquedaSucursal.NoEncontrada();
        }

        // Colapsa espacios en blanco repetidos.
        private static string ColapsarEspacios(string texto) =>
            string.IsNullOrWhiteSpace(texto)
                ? string.Empty
                : System.Text.RegularExpressions.Regex.Replace(texto.Trim(), @"\s+", " ");

        // Quita acentos de un texto
        private static string QuitarAcentos(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            var normalizadoFormD = texto.Normalize(System.Text.NormalizationForm.FormD);
            var resultado = new System.Text.StringBuilder();

            foreach (char ch in normalizadoFormD)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                    resultado.Append(ch);
            }

            return resultado.ToString().Normalize(System.Text.NormalizationForm.FormC);
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
                    sql.Append(" AND dbr.F_Inicio > '1900-01-01' AND MONTH(dbr.F_Inicio) BETWEEN @MesIni AND @MesFin");
                }
                else
                {
                    sql.Append(" AND dbr.F_Inicio > '1900-01-01' AND (MONTH(dbr.F_Inicio) >= @MesIni OR MONTH(dbr.F_Inicio) <= @MesFin)");
                }
                cmd.Parameters.AddWithValue("@MesIni", filtroMesInicio.Value);
                cmd.Parameters.AddWithValue("@MesFin", filtroMesFin.Value);
            }
            else if (filtroMesInicio.HasValue)
            {
                sql.Append(" AND dbr.F_Inicio > '1900-01-01' AND MONTH(dbr.F_Inicio) = @MesIni");
                cmd.Parameters.AddWithValue("@MesIni", filtroMesInicio.Value);
            }
            else if (filtroMesFin.HasValue)
            {
                sql.Append(" AND dbr.F_Inicio > '1900-01-01' AND MONTH(dbr.F_Inicio) = @MesFin");
                cmd.Parameters.AddWithValue("@MesFin", filtroMesFin.Value);
            }
            if (ocultarSinFecha)
            {
                sql.Append(" AND dbr.F_Inicio IS NOT NULL AND dbr.F_Inicio > '1900-01-01'");
            }

            if (filtroMesInicio.HasValue)
            {
                sql.Append(" ORDER BY (MONTH(dbr.F_Inicio) - @MesIni + 12) % 12, suc.RUTA, suc.Sucursal");
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
