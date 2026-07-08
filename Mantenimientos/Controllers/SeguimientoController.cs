using ClosedXML.Excel;
using Mantenimientos.Data;
using Mantenimientos.Models;
using Mantenimientos.Models.ViewModels;
using Mantenimientos.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Mantenimientos.Controllers
{
    public class SeguimientoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmpDataService _empDataService;
        private readonly PeriodoService _periodoService;
        private readonly ILogger<SeguimientoController> _logger;
        private readonly IConfiguration _configuration;

        public SeguimientoController(
            ApplicationDbContext context,
            EmpDataService empDataService,
            PeriodoService periodoService,
            ILogger<SeguimientoController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _empDataService = empDataService;
            _periodoService = periodoService;
            _logger = logger;
            _configuration = configuration;
        }

        // GET /Seguimiento/Index
        public async Task<IActionResult> Index(
            int? filtroRuta,
            string? filtroEmpresa,
            int? filtroMesInicio,
            int? filtroMesFin,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodoActual = await _periodoService.ObtenerPeriodoActualAsync();
            int periodoActivo = filtroPeriodo ?? periodoActual;

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                periodo: periodoActivo,
                filtroRuta: filtroRuta,
                filtroEmpresa: filtroEmpresa,
                filtroMesInicio: filtroMesInicio,
                filtroMesFin: filtroMesFin,
                ocultarSinFecha: ocultarSinFecha);

            var listaRutas = await _empDataService.ObtenerRutasAsync();
            var listaPeriodos = await _periodoService.ObtenerPeriodosDisponiblesAsync();
            var meses = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = new DateTime(2000, m, 1).ToString("MMMM")
                }).ToList();

            static string NombreRegion(int id) => id switch
            {
                11 => "Central",
                12 => "México",
                13 => "Norte",
                14 => "Sureste",
                _ => $"Región {id}"
            };

            var viewModel = new IndexVM
            {
                PeriodoActual = periodoActual,
                FiltroPeriodo = periodoActivo,
                FiltroRuta = filtroRuta,
                FiltroEmpresa = filtroEmpresa,
                FiltroMesInicio = filtroMesInicio,
                FiltroMesFin = filtroMesFin,
                ReporteUrl = _configuration["ReporteUrl"] ?? "http://192.168.1.5",

                Seguimientos = datos.Select(d => new SeguimientoViewModel
                {
                    ID = d.ID,
                    CLV_SUC = d.CLV_SUC,
                    ID_PERIODO = d.ID_PERIODO,
                    SUCURSAL = d.SUCURSAL,
                    RUTA = d.RUTA,
                    REGION = d.REGION,
                    REGION_NOMBRE = NombreRegion(d.REGION),
                    FECHA_INI_ES = d.FECHA_INI_ES,
                    FECHA_FIN_ES = d.FECHA_FIN_ES,
                    FECHA_INI_RE = d.FECHA_INI_RE,
                    FECHA_FIN_RE = d.FECHA_FIN_RE,
                    OBSERVACIONES = d.OBSERVACIONES
                }).ToList(),

                RutasDisponibles = listaRutas.Select(r => new SelectListItem
                {
                    Value = r.ToString(),
                    Text = r.ToString(),
                    Selected = r == filtroRuta
                }).ToList(),

                PeriodosDisponibles = listaPeriodos.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Nombre,
                    Selected = p.Id == periodoActivo
                }).ToList(),

                MesesDisponibles = meses
            };

            ViewBag.OcultarSinFecha = ocultarSinFecha;
            return View(viewModel);
        }

        // GET /Seguimiento/Observacion/{id}
        [HttpGet]
        public async Task<IActionResult> Observacion(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
                return RedirectToAction(nameof(Index));

            var seguimiento = await _context.Seguimientos.FindAsync(id.Value);
            if (seguimiento == null) return NotFound();

            var sucInfo = await _empDataService.ObtenerInfoSucursalAsync(seguimiento.CLV_SUC);
            var fechasReales = await _empDataService.ObtenerFechasRealesAsync(
                seguimiento.CLV_SUC, seguimiento.ID_PERIODO);

            var vm = new ObservacionVM
            {
                ID = seguimiento.ID,
                CLV_SUC = seguimiento.CLV_SUC,
                ID_PERIODO = seguimiento.ID_PERIODO,
                SUCURSAL = sucInfo?.Nombre ?? seguimiento.CLV_SUC,
                RUTA = sucInfo?.RUTA ?? 0,
                REGION = sucInfo?.REGION ?? 0,
                REGION_NOMBRE = (sucInfo?.REGION ?? 0) switch
                {
                    11 => "Central",
                    12 => "México",
                    13 => "Norte",
                    14 => "Sureste",
                    _ => $"Región {sucInfo?.REGION ?? 0}"
                },
                FECHA_INI_ES = seguimiento.FECHA_INI_ES,
                FECHA_FIN_ES = seguimiento.FECHA_FIN_ES,
                FECHA_INI_RE = fechasReales?.FechaInicio,
                FECHA_FIN_RE = fechasReales?.FechaFin,
                OBSERVACIONES = seguimiento.OBSERVACIONES
            };
            return View(vm);
        }

        // POST /Seguimiento/Observacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Observacion(ObservacionVM model)
        {
            ModelState.Remove(nameof(ObservacionVM.SUCURSAL));
            ModelState.Remove(nameof(ObservacionVM.RUTA));
            ModelState.Remove(nameof(ObservacionVM.REGION));
            ModelState.Remove(nameof(ObservacionVM.REGION_NOMBRE));
            ModelState.Remove(nameof(ObservacionVM.FECHA_INI_RE));
            ModelState.Remove(nameof(ObservacionVM.FECHA_FIN_RE));

            if (!ModelState.IsValid)
            {
                var sucInfo = await _empDataService.ObtenerInfoSucursalAsync(model.CLV_SUC);
                var fechasReales = await _empDataService.ObtenerFechasRealesAsync(model.CLV_SUC, model.ID_PERIODO);
                model.SUCURSAL = sucInfo?.Nombre ?? model.CLV_SUC;
                model.RUTA = sucInfo?.RUTA ?? 0;
                model.REGION = sucInfo?.REGION ?? 0;
                model.FECHA_INI_RE = fechasReales?.FechaInicio;
                model.FECHA_FIN_RE = fechasReales?.FechaFin;
                return View(model);
            }

            try
            {
                var existente = await _context.Seguimientos.FindAsync(model.ID);
                if (existente is null) return NotFound();

                existente.FECHA_INI_ES = model.FECHA_INI_ES;
                existente.FECHA_FIN_ES = model.FECHA_FIN_ES;
                existente.OBSERVACIONES = model.OBSERVACIONES;

                _context.Seguimientos.Update(existente);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Registro actualizado con éxito.";
                TempData["TipoAlerta"] = "success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar observación ID={ID}.", model.ID);
                TempData["Mensaje"] = "Error al guardar. Intente de nuevo.";
                TempData["TipoAlerta"] = "danger";
            }
            return RedirectToAction(nameof(Index), new { filtroPeriodo = model.ID_PERIODO });
        }

        // GET /Seguimiento/Admin
        [HttpGet]
        public async Task<IActionResult> Admin()
        {
            int periodoActual = await _periodoService.ObtenerPeriodoActualAsync();
            ViewBag.PeriodoActual = periodoActual;
            return View();
        }

        // POST /Seguimiento/Importar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar()
        {
            try
            {
                int periodoActual = await _periodoService.ObtenerPeriodoActualAsync();
                const string sql = @"
                    INSERT INTO mttos.dbo.Seguimientos (CLV_SUC, ID_PERIODO)
                    SELECT suc.CLV_SUC, @PeriodoActual
                    FROM Iker.dbo.Sucursales AS suc
                    WHERE suc.ACTIVO = 1
                    AND NOT EXISTS (SELECT 1 FROM mttos.dbo.Seguimientos AS s
                    WHERE s.CLV_SUC = suc.CLV_SUC AND s.ID_PERIODO = @PeriodoActual);";

                int insertados = await _context.Database.ExecuteSqlRawAsync(
                    sql, new SqlParameter("@PeriodoActual", periodoActual));

                TempData["Mensaje"] = $"Importación exitosa — {insertados} sucursales agregadas para el Periodo {periodoActual}.";
                TempData["TipoAlerta"] = "success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al importar.");
                TempData["Mensaje"] = "Error al importar sucursales.";
                TempData["TipoAlerta"] = "danger";
            }
            return RedirectToAction(nameof(Admin));
        }

        // GET /Seguimiento/Exportar
        [HttpGet]
        public async Task<IActionResult> Exportar(
            int? filtroRuta,
            string? filtroEmpresa,
            int? filtroMesInicio,
            int? filtroMesFin,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodoActual = await _periodoService.ObtenerPeriodoActualAsync();
            int periodo = filtroPeriodo ?? periodoActual;

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                periodo: periodo,
                filtroRuta: filtroRuta,
                filtroEmpresa: filtroEmpresa,
                filtroMesInicio: filtroMesInicio,
                filtroMesFin: filtroMesFin,
                ocultarSinFecha: ocultarSinFecha);

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");
            hoja.Style.Font.FontName = "Arial";
            hoja.Style.Font.FontSize = 10;

            hoja.Cell("C3").Value = "Sucursal"; hoja.Range("C3:C4").Merge();
            hoja.Cell("D3").Value = "Fecha Estimada"; hoja.Range("D3:E3").Merge();
            hoja.Cell("F3").Value = "Fecha Real"; hoja.Range("F3:G3").Merge();
            hoja.Cell("H3").Value = "Días desfasados"; hoja.Range("H3:H4").Merge();
            hoja.Cell("I3").Value = "Observaciones"; hoja.Range("I3:I4").Merge();
            hoja.Cell("D4").Value = "Inicio";
            hoja.Cell("E4").Value = "Fin";
            hoja.Cell("F4").Value = "Inicio";
            hoja.Cell("G4").Value = "Fin";

            var rango = hoja.Range("C3:I4");
            rango.Style.Font.SetBold(true)
                 .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                 .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            rango.Cells().Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
            rango.Cells().Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            int fila = 5;
            foreach (var d in datos)
            {
                hoja.Cell(fila, "C").Value = d.SUCURSAL;
                hoja.Cell(fila, "D").Value = FormatFechaExcel(d.FECHA_INI_ES);
                hoja.Cell(fila, "E").Value = FormatFechaExcel(d.FECHA_FIN_ES);
                hoja.Cell(fila, "F").Value = FormatFechaExcel(d.FECHA_INI_RE);
                hoja.Cell(fila, "G").Value = FormatFechaExcel(d.FECHA_FIN_RE);
                hoja.Cell(fila, "H").Value = d.Dias;
                hoja.Cell(fila, "I").Value = d.OBSERVACIONES ?? string.Empty;
                hoja.Range(fila, 3, fila, 9).Style
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetInsideBorder(XLBorderStyleValues.Thin);
                hoja.Range(fila, 4, fila, 8).Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                fila++;
            }
            hoja.Columns("C:I").AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Fechas_P{periodo}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // GET /Seguimiento/Descargar
        [HttpGet]
        public IActionResult Descargar()
        {
            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Fechas");
            hoja.Style.Font.FontName = "Arial";
            hoja.Style.Font.FontSize = 11;

            hoja.Cell("C3").Value = "Sucursal";
            hoja.Cell("D3").Value = "Fecha Inicio";
            hoja.Cell("E3").Value = "Fecha Fin";

            var rango = hoja.Range("C3:E3");
            rango.Style.Font.SetBold(true)
                 .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                 .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            rango.Cells().Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
            rango.Cells().Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Formato.xlsx");
        }

        // POST /Seguimiento/Cargar
        [HttpPost]
        public async Task<IActionResult> Cargar(IFormFile archivo, int? filtroPeriodo)
        {
            _logger.LogInformation($"Nombre: {archivo.FileName}");
            _logger.LogInformation($"Extensión: {Path.GetExtension(archivo.FileName)}");
            _logger.LogInformation($"ContentType: {archivo.ContentType}");
            if (archivo == null || archivo.Length == 0)
            {
                TempData["Mensaje"] = "Debes seleccionar un archivo Excel (.xlsx) antes de subir.";
                TempData["TipoAlerta"] = "warning";
                return RedirectToAction(nameof(Index), new { filtroPeriodo });
            }

            var extension = Path.GetExtension(archivo.FileName);

            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Mensaje"] = "Solo se permiten archivos de Excel.";
                TempData["TipoAlerta"] = "warning";
                return RedirectToAction(nameof(Index), new { filtroPeriodo });
            }

            // Verificación adicional por contenido real del archivo (además de la extensión)
            if (!await EsArchivoExcelValidoAsync(archivo))
            {
                TempData["Mensaje"] = "El archivo no es un Excel válido. Verifica que no esté dañado ni sea otro tipo de archivo renombrado.";
                TempData["TipoAlerta"] = "warning";
                return RedirectToAction(nameof(Index), new { filtroPeriodo });
            }

            try
            {
                int periodoActual = await _periodoService.ObtenerPeriodoActualAsync();
                int periodo = filtroPeriodo ?? periodoActual;

                var resultado = new ExcelUpDto();

                using var stream = archivo.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var hoja = workbook.Worksheets.First();

                int primeraFila = 4;
                int ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? primeraFila;

                _logger.LogInformation($"Procesando Excel: Primera fila {primeraFila}, Última fila {ultimaFila}, Período {periodo}");

                for (int f = primeraFila; f <= ultimaFila; f++)
                {
                    string nombreCelda = hoja.Cell(f, 3).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(nombreCelda))
                        continue;

                    resultado.TotalFilas++;

                    DateTime? fechaIni = LeerFechaExcel(hoja.Cell(f, 4));
                    DateTime? fechaFin = LeerFechaExcel(hoja.Cell(f, 5));

                    _logger.LogInformation($"Fila {f}: Sucursal='{nombreCelda}', FechaIni={fechaIni?.ToString("dd/MM/yyyy") ?? "null"}, FechaFin={fechaFin?.ToString("dd/MM/yyyy") ?? "null"}");

                    string? clvSuc = await _empDataService.BuscarClvSucPorNombreAsync(nombreCelda);

                    if (clvSuc == null)
                    {
                        resultado.NoEncontrados++;
                        resultado.NombresNoEncontrados.Add($"'{nombreCelda}' (no encontrada)");
                        _logger.LogWarning($"Sucursal no encontrada: {nombreCelda}");
                        continue;
                    }

                    var seguimiento = await _context.Seguimientos
                        .FirstOrDefaultAsync(s => s.CLV_SUC == clvSuc && s.ID_PERIODO == periodo);

                    if (seguimiento == null)
                    {
                        resultado.NoEncontrados++;
                        resultado.NombresNoEncontrados.Add($"'{nombreCelda}' (sin registro en período {periodo})");
                        _logger.LogWarning($"Seguimiento no existe para CLV_SUC={clvSuc}, Período={periodo}");
                        continue;
                    }

                    // Solo actualizar si hay al menos una fecha válida
                    bool tieneAlgunaFecha = fechaIni.HasValue || fechaFin.HasValue;
                    if (tieneAlgunaFecha)
                    {
                        seguimiento.FECHA_INI_ES = fechaIni;
                        seguimiento.FECHA_FIN_ES = fechaFin;
                        resultado.Actualizados++;
                        _logger.LogInformation($"Actualizado: CLV_SUC={clvSuc}, FechaIni={fechaIni?.ToString("dd/MM/yyyy") ?? "null"}, FechaFin={fechaFin?.ToString("dd/MM/yyyy") ?? "null"}");
                    }
                    else
                    {
                        _logger.LogWarning($"Sin fechas válidas para: {nombreCelda} (CLV_SUC={clvSuc})");
                    }
                }

                await _context.SaveChangesAsync();

                // mensaje de resultado 
                var msg = new System.Text.StringBuilder();
                msg.Append($"Excel procesado: <strong>{resultado.Actualizados}</strong> ");
                msg.Append($"de {resultado.TotalFilas} filas actualizadas para el Periodo {periodo}.");

                if (resultado.NombresNoEncontrados.Any())
                {
                    msg.Append($" | <strong>{resultado.NoEncontrados} no encontradas:</strong> ");
                    msg.Append(string.Join(", ", resultado.NombresNoEncontrados.Take(10)));
                    if (resultado.NombresNoEncontrados.Count > 10)
                        msg.Append($" … y {resultado.NombresNoEncontrados.Count - 10} más.");
                }

                TempData["Mensaje"] = msg.ToString();
                TempData["TipoAlerta"] = resultado.Actualizados > 0 ? "success" : "warning";
                TempData["EsHtml"] = "true";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar Excel.");
                TempData["Mensaje"] = $"Error al procesar el archivo: {ex.Message}";
                TempData["TipoAlerta"] = "danger";
            }

            return RedirectToAction(nameof(Index), new { filtroPeriodo });
        }

        // AJAX GET /Seguimiento/ObtenerSucursalesFiltro?ruta=X
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursalesFiltro(int ruta)
        {
            var suc = await _empDataService.ObtenerSucursalesPorRutaAsync(ruta);
            return Json(suc.Select(s => new { value = s.CLV_SUC, text = s.Nombre }));
        }

        // Helpers
        private static async Task<bool> EsArchivoExcelValidoAsync(IFormFile archivo)
        {
            try
            {
                var buffer = new byte[8];
                await using var stream = archivo.OpenReadStream();
                int leidos = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (leidos < 4) return false;

                bool esZipXlsx = buffer[0] == 0x50 && buffer[1] == 0x4B;
                bool esOleXls = buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0;

                if (!esZipXlsx && !esOleXls)
                    return false;

                stream.Position = 0;
                using var workbook = new XLWorkbook(stream);
                _ = workbook.Worksheets.First();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatFechaExcel(DateTime? f) =>
            f.HasValue ? f.Value.ToString("dd/MM/yyyy") : string.Empty;

        private static DateTime? LeerFechaExcel(IXLCell celda)
        {
            if (celda.IsEmpty()) return null;

            if (celda.DataType == XLDataType.DateTime)
                return celda.GetDateTime();

            // Intentar parsear como texto
            var texto = celda.GetString().Trim();
            if (string.IsNullOrEmpty(texto)) return null;

            var culture = new System.Globalization.CultureInfo("es-ES");

            string[] formatosConDia = {
                "ddd dd/MM/yy",
                "ddd dd/MM/yyyy",
                "ddd d/M/yy",
                "ddd d/M/yyyy"
            };

            if (DateTime.TryParseExact(texto, formatosConDia, culture,
                    System.Globalization.DateTimeStyles.None, out var fechaConDia))
                return fechaConDia;

            string[] formatos = {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "dd/MM/yy",
                "d/M/yy"
            };

            if (DateTime.TryParseExact(texto, formatos,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var fecha))
                return fecha;

            if (DateTime.TryParse(texto, culture,
                    System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var fecha2))
                return fecha2;

            return null;
        }
    }
}