using ClosedXML.Excel;
using Mantenimientos.Data;
using Mantenimientos.Models;
using Mantenimientos.Models.ViewModels;
using Mantenimientos.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Mantenimientos.Controllers
{
    public class SeguimientoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmpDataService _empDataService;
        private readonly ILogger<SeguimientoController> _logger;

        //Fecha por defecto para valores nulos
        private static readonly DateTime FechaDefault = new(1900, 1, 1);
        public SeguimientoController(
            ApplicationDbContext context,
            EmpDataService empDataService,
            ILogger<SeguimientoController> logger)
        {
            _context = context;
            _empDataService = empDataService;
            _logger = logger;
        }

        // Modulo de consulta
        public async Task<IActionResult> Index(
            int? filtroRuta,
            string? filtroSucursal,
            int? filtroMes,
            int? filtroAnio)
        {
            // Consulta con filtros
            var query = _context.Seguimientos.AsQueryable();
            if (filtroRuta.HasValue)
                query = query.Where(s => s.RUTA == filtroRuta.Value);
            if (!string.IsNullOrEmpty(filtroSucursal))
                query = query.Where(s => s.SUCURSAL == filtroSucursal);
            if (filtroMes.HasValue)
                query = query.Where(s => s.FECHA_INI_ES.Month == filtroMes.Value && s.FECHA_INI_ES != FechaDefault);
            if (filtroAnio.HasValue)
                query = query.Where(s => s.FECHA_INI_ES.Year == filtroAnio.Value && s.FECHA_INI_ES != FechaDefault);

            //proyeccion a ViewModel
            var seguimientos = await query
                .OrderBy(s => s.RUTA)
                .ThenBy(s => s.SUCURSAL)
                .Select(s => new SeguimientoViewModel
                {
                    RUTA = s.RUTA,
                    SUCURSAL = s.SUCURSAL,
                    FECHA_INI_ES = s.FECHA_INI_ES,
                    FECHA_FIN_ES = s.FECHA_FIN_ES,
                    FECHA_INI_RE = s.FECHA_INI_RE,
                    FECHA_FIN_RE = s.FECHA_FIN_RE,
                    DIAS_ATRASO = s.DIAS_ATRASO,
                    OBSERVACIONES = s.OBSERVACIONES
                })
                .ToListAsync();

            // Listas para filtros
            var todasRutas = await _context.Seguimientos
                .Select(r => r.RUTA)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();
            var todasSucursales = await _context.Seguimientos
                .Select(s => s.SUCURSAL)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
            var meses = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = new DateTime(2000, m, 1).ToString("MMMM")
                })
                .ToList();
            var viewModel = new IndexVM
            {
                Seguimientos = seguimientos,
                FiltroRuta = filtroRuta,
                FiltroSucursal = filtroSucursal,
                FiltroMes = filtroMes,
                FiltroAnio = filtroAnio,
                RutasDisponibles = todasRutas
                    .Select(r => new SelectListItem
                    {
                        Value = r.ToString(),
                        Text = r.ToString(),
                        Selected = filtroRuta.HasValue && r == filtroRuta.Value
                    }).ToList(),
                SucursalesDisponibles = todasSucursales
                    .Select(s => new SelectListItem
                    {
                        Value = s,
                        Text = s,
                        Selected = s == filtroSucursal
                    }).ToList(),
                MesesDisponibles = meses
            };
            return View(viewModel);
        }

        //Modulo de agregar observaciones
        //GET /Seguimiento/Observacion/{id?} para editar
        [HttpGet]
        public async Task<IActionResult> Observacion(int? id)
        {
            ObservacionVM viewModel = new ObservacionVM();
            if (id.HasValue && id.Value > 0)
            {
                var seguimiento = await _context.Seguimientos.FindAsync(id.Value);
                if (seguimiento == null)
                {
                    return NotFound();
                }
                viewModel = new ObservacionVM
                {
                    RUTA = seguimiento.RUTA,
                    SUCURSAL = seguimiento.SUCURSAL,
                    FECHA_INI_ES = seguimiento.FECHA_INI_ES != FechaDefault ? seguimiento.FECHA_INI_ES : null,
                    FECHA_FIN_ES = seguimiento.FECHA_FIN_ES != FechaDefault ? seguimiento.FECHA_FIN_ES : null,
                    FECHA_INI_RE = seguimiento.FECHA_INI_RE != FechaDefault ? seguimiento.FECHA_INI_RE : null,
                    FECHA_FIN_RE = seguimiento.FECHA_FIN_RE != FechaDefault ? seguimiento.FECHA_FIN_RE : null,
                    OBSERVACIONES = seguimiento.OBSERVACIONES
                };
            }
            else
            {
                //Formulario vacio
                viewModel = new ObservacionVM();
            }
            await CargarDropdownAsync(viewModel);
            return View(viewModel);
        }
        //POST /Seguimiento/Observacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Observacion(ObservacionVM model)
        {
            if (!ModelState.IsValid)
            {
                await CargarDropdownAsync(model);
                return View(model);
            }

            //Asignar fecha por defecto
            var fechaIniEst = model.FECHA_INI_ES ?? FechaDefault;
            var fechaFinEst = model.FECHA_FIN_ES ?? FechaDefault;
            var fechaIniReal = model.FECHA_INI_RE ?? FechaDefault;
            var fechaFinReal = model.FECHA_FIN_RE ?? FechaDefault;

            //Calcular dias de atraso
            int diasDesfasados = 0;
            if (fechaFinReal != FechaDefault &&
                fechaFinEst != FechaDefault)
            {
                diasDesfasados = (int)(fechaFinReal - fechaFinEst).TotalDays;
            }
            // Nuuevo registro o actualización
            try
            {
                if (model.Id == 0)
                {
                    var nuevo = new Seguimiento
                    {
                        RUTA = model.RUTA,
                        SUCURSAL = model.SUCURSAL,
                        FECHA_INI_ES = fechaIniEst,
                        FECHA_FIN_ES = fechaFinEst,
                        FECHA_INI_RE = fechaIniReal,
                        FECHA_FIN_RE = fechaFinReal,
                        DIAS_ATRASO = diasDesfasados,
                        OBSERVACIONES = model.OBSERVACIONES?.Trim()
                    };

                    _context.Seguimientos.Add(nuevo);
                    TempData["Mensaje"] = "Observación guardada correctamente.";
                    TempData["TipoAlerta"] = "success";
                }
                else
                {
                    // Actualizar registro existente
                    var existente = await _context.Seguimientos
                                                  .FindAsync(model.Id);
                    if (existente is null)
                        return NotFound();

                    existente.Ruta = model.Ruta;
                    existente.Sucursal = model.Sucursal;
                    existente.FechaInicioEstimada = fechaIniEst;
                    existente.FechaFinEstimada = fechaFinEst;
                    existente.FechaInicioReal = fechaIniReal;
                    existente.FechaFinReal = fechaFinReal;
                    existente.DiasDesfasados = diasDesfasados;
                    existente.Observaciones = model.Observaciones?.Trim();

                    _context.SeguimientoMantenimientos.Update(existente);
                    TempData["Mensaje"] = "✅ Observación actualizada correctamente.";
                    TempData["TipoAlerta"] = "success";
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar observación.");
                TempData["Mensaje"] = "❌ Error al guardar. Intente nuevamente.";
                TempData["TipoAlerta"] = "danger";
            }

            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════════════════
        // EXPORTAR A EXCEL
        // GET: /Seguimiento/ExportarExcel
        // ═══════════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ExportarExcel(
            string? filtroRuta,
            string? filtroSucursal,
            int? filtroMes)
        {
            var query = _context.SeguimientoMantenimientos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtroRuta))
                query = query.Where(s => s.Ruta == filtroRuta);

            if (!string.IsNullOrWhiteSpace(filtroSucursal))
                query = query.Where(s => s.Sucursal == filtroSucursal);

            if (filtroMes.HasValue)
                query = query.Where(s =>
                    s.FechaInicioEstimada.Month == filtroMes.Value &&
                    s.FechaInicioEstimada != FechaDefault);

            var datos = await query
                .OrderBy(s => s.Ruta)
                .ThenBy(s => s.Sucursal)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");

            // ── Encabezado del reporte ────────────────────────────────
            hoja.Cell(1, 1).Value = $"Reporte de Mantenimientos — Grupo Bimbo";
            hoja.Range(1, 1, 1, 8).Merge()
                .Style.Font.SetBold(true)
                .Font.SetFontSize(14)
                .Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#1D3557"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            hoja.Cell(2, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            hoja.Range(2, 1, 2, 8).Merge()
                .Style.Font.SetItalic(true)
                .Font.SetFontColor(XLColor.Gray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // ── Cabecera de columnas ──────────────────────────────────
            string[] encabezados =
            {
                "RUTA", "SUCURSAL", "FECHA_INI_ES", "FECHA_FIN_ES",
                "FECHA_INI_RE", "FECHA_FIN_RE", "DIAS_ATRASO", "OBSERVACIONES"
            };

            for (int col = 0; col < encabezados.Length; col++)
            {
                var celda = hoja.Cell(4, col + 1);
                celda.Value = encabezados[col];
                celda.Style
                    .Font.SetBold(true)
                    .Font.SetFontColor(XLColor.White)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#457B9D"))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetOutsideBorderColor(XLColor.White);
            }

            // ── Filas de datos ────────────────────────────────────────
            for (int i = 0; i < datos.Count; i++)
            {
                var s = datos[i];
                int row = i + 5;

                hoja.Cell(row, 1).Value = s.Ruta;
                hoja.Cell(row, 2).Value = s.Sucursal;
                hoja.Cell(row, 3).Value = FormatFechaExcel(s.FechaInicioEstimada);
                hoja.Cell(row, 4).Value = FormatFechaExcel(s.FechaFinEstimada);
                hoja.Cell(row, 5).Value = FormatFechaExcel(s.FechaInicioReal);
                hoja.Cell(row, 6).Value = FormatFechaExcel(s.FechaFinReal);
                hoja.Cell(row, 7).Value = s.DiasDesfasados;
                hoja.Cell(row, 8).Value = s.Observaciones ?? string.Empty;

                // Color alterno de filas
                var colorFondo = i % 2 == 0
                    ? XLColor.White
                    : XLColor.FromHtml("#F1FAEE");

                hoja.Range(row, 1, row, 8).Style
                    .Fill.SetBackgroundColor(colorFondo)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#CCCCCC"));

                // Colorear días de atraso
                if (s.DiasDesfasados > 0)
                    hoja.Cell(row, 7).Style
                        .Font.SetFontColor(XLColor.Red)
                        .Font.SetBold(true);
                else if (s.DiasDesfasados < 0)
                    hoja.Cell(row, 7).Style
                        .Font.SetFontColor(XLColor.DarkGreen)
                        .Font.SetBold(true);
            }

            // ── Resumen al final ──────────────────────────────────────
            int filaResumen = datos.Count + 6;
            hoja.Cell(filaResumen, 1).Value = $"Total registros: {datos.Count}";
            hoja.Cell(filaResumen, 7).Value = $"Promedio atraso: {(datos.Count > 0 ? datos.Average(d => d.DiasDesfasados) : 0):F1} días";

            // ── Autoajuste de columnas ────────────────────────────────
            hoja.Columns().AdjustToContents();
            hoja.Column(8).Width = 50; // Observaciones más ancha

            // ── Generar archivo y retornar ────────────────────────────
            await using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var nombreArchivo = $"Mantenimientos_Bimbo_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                nombreArchivo);
        }

        // ═══════════════════════════════════════════════════════════════
        // ENDPOINTS AJAX — Carga dinámica de selects
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Retorna sucursales para una ruta en formato JSON.</summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursales(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
                return Json(new List<string>());

            var sucursales = await _empresaService.ObtenerSucursalesPorRutaAsync(ruta);
            return Json(sucursales);
        }

        /// <summary>Retorna fechas estimadas para una Ruta+Sucursal en formato JSON.</summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerFechasEstimadas(string ruta, string sucursal)
        {
            if (string.IsNullOrWhiteSpace(ruta) || string.IsNullOrWhiteSpace(sucursal))
                return Json(new { });

            var fechas = await _empresaService.ObtenerFechasEstimadasAsync(ruta, sucursal);

            if (fechas is null)
                return Json(new { });

            return Json(new
            {
                fechaIniEst = fechas.FechaInicioEstimada == FechaDefault
                    ? string.Empty
                    : fechas.FechaInicioEstimada.ToString("yyyy-MM-dd"),
                fechaFinEst = fechas.FechaFinEstimada == FechaDefault
                    ? string.Empty
                    : fechas.FechaFinEstimada.ToString("yyyy-MM-dd")
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // MÉTODOS PRIVADOS AUXILIARES
        // ═══════════════════════════════════════════════════════════════

        private async Task CargarDropdownsAsync(ObservacionViewModel model)
        {
            // Rutas desde BD empresa
            try
            {
                var rutas = await _empresaService.ObtenerRutasAsync();
                model.RutasDisponibles = rutas
                    .Select(r => new SelectListItem { Value = r, Text = r })
                    .ToList();

                // Si ya hay una ruta seleccionada, cargar sus sucursales
                if (!string.IsNullOrWhiteSpace(model.Ruta))
                {
                    var sucursales = await _empresaService.ObtenerSucursalesPorRutaAsync(model.Ruta);
                    model.SucursalesDisponibles = sucursales
                        .Select(s => new SelectListItem { Value = s, Text = s })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dropdowns de empresa.");
                model.RutasDisponibles = new List<SelectListItem>();
                model.SucursalesDisponibles = new List<SelectListItem>();
            }
        }

        private static string FormatFechaExcel(DateTime fecha) =>
            fecha == FechaDefault ? "N/A" : fecha.ToString("dd/MM/yyyy");
    }
}