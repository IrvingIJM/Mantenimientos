using ClosedXML.Excel;
using Mantenimientos.Data;
using Mantenimientos.Models;
using Mantenimientos.Models.ViewModels;
using Mantenimientos.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Mantenimientos.Controllers
{
    public class SeguimientoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmpDataService _empDataService;
        private readonly ILogger<SeguimientoController> _logger;

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

        // GET  /Seguimiento/Index
        public async Task<IActionResult> Index(
            int? filtroRuta,
            string? filtroRegion,
            int? filtroMes,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodo = filtroPeriodo ?? 7;

            try
            {
                await _empDataService.SincronizarDiasAtrasoAsync(periodo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la sincronización automática del Index.");
            }

            // Obtener registros con joins y filtros
            var seguimientos = await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta: filtroRuta,
                filtroRegion: filtroRegion,
                filtroMes: filtroMes,
                periodo: periodo,
                soloConFecha: ocultarSinFecha);

            // Listas para dropdowns
            var rutas = await _empDataService.ObtenerRutasAsync();
            var regiones = await _empDataService.ObtenerRegionesAsync();
            var periodos = await _empDataService.ObtenerPeriodosAsync();

            List<(int ClvSuc, string Nombre)> sucursales;
            if (filtroRuta.HasValue)
                sucursales = await _empDataService.ObtenerSucursalesPorRutaAsync(filtroRuta.Value);
            else
                sucursales = await _empDataService.ObtenerTodasSucursalesAsync();

            var meses = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = new DateTime(2000, m, 1).ToString("MMMM"),
                    Selected = filtroMes.HasValue && m == filtroMes.Value
                }).ToList();

            var anios = Enumerable.Range(2023, (DateTime.Now.Year - 2023) + 2)
                .OrderByDescending(a => a)
                .Select(a => new SelectListItem
                {
                    Value = a.ToString(),
                    Text = a.ToString(),
                }).ToList();

            ViewBag.OcultarSinFecha = ocultarSinFecha;

            var viewModel = new IndexVM
            {
                Seguimientos = seguimientos,
                FiltroRuta = filtroRuta,
                FiltroRegion = filtroRegion,
                FiltroMes = filtroMes,
                FiltroPeriodo = filtroPeriodo,

                RutasDisponibles = rutas.Select(r => new SelectListItem
                {
                    Value = r.ToString(),
                    Text = r.ToString(),
                    Selected = filtroRuta.HasValue && r == filtroRuta.Value
                }).ToList(),

                RegionesDisponibles = regiones.Select(r => new SelectListItem
                {
                    Value = r,
                    Text = r,
                    Selected = r == filtroRegion
                }).ToList(),

                MesesDisponibles = meses,

                PeriodosDisponibles = periodos.Select(p => new SelectListItem
                {
                    Value = p.ToString(),
                    Text = $"Periodo {p}",
                    Selected = p == periodo
                }).ToList()
            };

            return View(viewModel);
        }

        // GET  /Seguimiento/Observacion/{id}
        [HttpGet]
        public async Task<IActionResult> Observacion(int? id, int periodo = 7)
        {
            if (!id.HasValue || id.Value <= 0)
                return RedirectToAction(nameof(Index));

            var viewModel = await _empDataService.ObtenerDatosObservacionAsync(id.Value, periodo);
            if (viewModel == null)
                return NotFound();

            return View(viewModel);
        }

        // POST /Seguimiento/Observacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Observacion(ObservacionVM model, int periodo = 7)
        {
            // liite de 200 palabras en observaciones
            if (!string.IsNullOrWhiteSpace(model.OBSERVACIONES))
            {
                var palabras = model.OBSERVACIONES
                    .Trim()
                    .Split(new[] { ' ', '\n', '\r', '\t' },
                           StringSplitOptions.RemoveEmptyEntries);
                if (palabras.Length > 200)
                {
                    ModelState.AddModelError("OBSERVACIONES",
                        "Las observaciones no pueden superar las 200 palabras.");
                }
            }

            if (!ModelState.IsValid)
            {
                var recarga = await _empDataService.ObtenerDatosObservacionAsync(model.ID, periodo);
                if (recarga != null)
                {
                    model.RUTA = recarga.RUTA;
                    model.SUCURSAL = recarga.SUCURSAL;
                    model.REGION = recarga.REGION;
                    model.FECHA_INI_RE = recarga.FECHA_INI_RE;
                    model.FECHA_FIN_RE = recarga.FECHA_FIN_RE;
                    model.DIAS_ATRASO = recarga.DIAS_ATRASO;
                }
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

                TempData["Mensaje"] = "Observación actualizada con éxito.";
                TempData["TipoAlerta"] = "success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar observación.");
                TempData["Mensaje"] = "Error al guardar los cambios.";
                TempData["TipoAlerta"] = "danger";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /Seguimiento/Importar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar()
        {
            const string sqlQuery = @"
                INSERT INTO mttos.dbo.Seguimientos (CLV_SUC)
                SELECT origen.CLV_SUC
                FROM Iker.dbo.Sucursales AS origen
                WHERE origen.ACTIVO = 1
                  AND NOT EXISTS (
                    SELECT 1
                    FROM mttos.dbo.Seguimientos AS destino
                    WHERE destino.CLV_SUC = origen.CLV_SUC
                  );";

            return RedirectToAction(nameof(Index));
        }

        // GET  /Seguimiento/Exportar
        [HttpGet]
        public async Task<IActionResult> Exportar(
            int? filtroRuta,
            string? filtroRegion,
            int? filtroClvSuc,
            int? filtroMes,
            int? filtroAnio,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodo = filtroPeriodo ?? 7;

            try { await _empDataService.SincronizarDiasAtrasoAsync(periodo); }
            catch (Exception ex) { _logger.LogError(ex, "Error en actualización previa a Excel."); }

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta: filtroRuta,
                filtroRegion: filtroRegion,
                filtroMes: filtroMes,
                periodo: periodo,
                soloConFecha: ocultarSinFecha);

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");

            hoja.Style.Font.FontName = "Arial";
            hoja.Style.Font.FontSize = 10;
            hoja.SetShowGridLines(true);

            hoja.Cell("B3").Value = "Ruta";
            hoja.Range("B3:B4").Merge();

            hoja.Cell("D3").Value = "Centro de Ventas";
            hoja.Range("D3:D4").Merge();

            hoja.Cell("E3").Value = "Fecha Estimada";
            hoja.Range("E3:F3").Merge();

            hoja.Cell("G3").Value = "Fecha Real";
            hoja.Range("G3:H3").Merge();

            hoja.Cell("I3").Value = "Días Desfasados";
            hoja.Range("I3:I4").Merge();

            hoja.Cell("J3").Value = "Observaciones";
            hoja.Range("J3:J4").Merge();

            hoja.Cell("E4").Value = "Inicio";
            hoja.Cell("F4").Value = "Fin";
            hoja.Cell("G4").Value = "Inicio";
            hoja.Cell("H4").Value = "Fin";

            var rangoEnc = hoja.Range("B3:J4");
            rangoEnc.Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            rangoEnc.Cells().Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
            rangoEnc.Cells().Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            int fila = 5;
            foreach (var s in datos)
            {
                hoja.Cell(fila, "B").Value = s.RUTA;
                hoja.Cell(fila, "D").Value = s.SUCURSAL;
                hoja.Cell(fila, "E").Value = FormatFechaExcel(s.FECHA_INI_ES);
                hoja.Cell(fila, "F").Value = FormatFechaExcel(s.FECHA_FIN_ES);
                hoja.Cell(fila, "G").Value = FormatFechaExcel(s.FECHA_INI_RE);
                hoja.Cell(fila, "H").Value = FormatFechaExcel(s.FECHA_FIN_RE);
                hoja.Cell(fila, "I").Value = s.DIAS_ATRASO;
                hoja.Cell(fila, "J").Value = s.OBSERVACIONES ?? string.Empty;

                hoja.Range(fila, 2, fila, 3).Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                hoja.Cell(fila, "D").Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                hoja.Range(fila, 5, fila, 9).Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                hoja.Cell(fila, "J").Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                hoja.Range(fila, 2, fila, 10).Style.Border
                    .SetOutsideBorder(XLBorderStyleValues.Thin)
                    .SetInsideBorder(XLBorderStyleValues.Thin);
                fila++;
            }

            hoja.Columns("B:J").AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Mantenimientos_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // GET  /Seguimiento/ObtenerSucursalesFiltro?ruta=X
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursalesFiltro(int ruta)
        {
            var lista = await _empDataService.ObtenerSucursalesPorRutaAsync(ruta);
            return Json(lista.Select(s => new { clvSuc = s.ClvSuc, nombre = s.Nombre }));
        }

        private static string FormatFechaExcel(DateTime? fecha)
            => (!fecha.HasValue || fecha.Value <= FechaDefault)
               ? string.Empty
               : fecha.Value.ToString("dd/MM/yyyy");
    }
}