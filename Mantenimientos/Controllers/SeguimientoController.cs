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
        private readonly PeriodoService _periodoService;
        private readonly ILogger<SeguimientoController> _logger;

        public SeguimientoController(
            ApplicationDbContext context,
            EmpDataService empDataService,
            PeriodoService periodoService,
            ILogger<SeguimientoController> logger)
        {
            _context = context;
            _empDataService = empDataService;
            _periodoService = periodoService;
            _logger = logger;
        }
        // GET  /Seguimiento/Index
        public async Task<IActionResult> Index(
            int? filtroRuta,
            int? filtroMes,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            // 2. Obtenemos el periodo actual desde tu SP
            int periodoActualSP = await _periodoService.ObtenerPeriodoActualAsync();

            // 3. Calculamos el periodo anterior (asumiendo que si es 1, no baja a 0, o ajusta tu lógica si los periodos se reinician por año)
            int periodoAnteriorSP = periodoActualSP > 1 ? periodoActualSP - 1 : 1;

            // Si el usuario no mandó filtro, usamos el actual por defecto
            int periodoSeleccionado = filtroPeriodo ?? periodoActualSP;

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta, filtroRegion, filtroMes, ocultarSinFecha, periodoSeleccionado);

            var listaRutas = await _empDataService.ObtenerRutasAsync();
            var listaRegiones = await _empDataService.ObtenerRegionesAsync();

            // 4. CREAMOS LA LISTA SOLO CON LOS DOS PERIODOS
            var listaPeriodos = new List<int> { periodoActualSP, periodoAnteriorSP }.Distinct().ToList();

            var meses = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = new DateTime(2000, m, 1).ToString("MMMM"),
                    Selected = filtroMes.HasValue && filtroMes.Value == m
                }).ToList();

            var viewModel = new IndexVM
            {
                Seguimientos = datos.Select(d => new SeguimientoViewModel
                {
                    ID = d.ID,
                    CLV_SUC = d.CLV_SUC,
                    SUCURSAL = d.SUCURSAL,
                    RUTA = d.RUTA,
                    REGION = d.REGION,
                    FECHA_INI_ES = d.FECHA_INI_ES,
                    FECHA_FIN_ES = d.FECHA_FIN_ES,
                    FECHA_INI_RE = d.FECHA_INI_RE,
                    FECHA_FIN_RE = d.FECHA_FIN_RE,
                    OBSERVACIONES = d.OBSERVACIONES
                }).ToList(),

                FiltroRuta = filtroRuta,
                FiltroMes = filtroMes,
                FiltroPeriodo = periodoSeleccionado,

                RutasDisponibles = listaRutas.Select(r => new SelectListItem
                {
                    Value = r.ToString(),
                    Text = "Ruta " + r.ToString(),
                    Selected = (r == filtroRuta)
                }).ToList(),

                // 5. Mapeamos nuestra lista de 2 elementos al SelectListItem
                PeriodosDisponibles = listaPeriodos.Select(p => new SelectListItem
                {
                    Value = p.ToString(),
                    Text = (p == periodoActualSP) ? $"Periodo {p} (Actual)" : $"Periodo {p} (Anterior)",
                    Selected = (p == periodoSeleccionado)
                }).ToList(),

                MesesDisponibles = meses
            };

            ViewBag.OcultarSinFecha = ocultarSinFecha;
            return View(viewModel);
        }

        // GET  /Seguimiento/Observacion/{id}
        [HttpGet]
        public async Task<IActionResult> Observacion(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
                return RedirectToAction(nameof(Index));

            var seguimiento = await _context.Seguimientos.FindAsync(id.Value);
            if (seguimiento == null)
                return NotFound();

            var sucInfo = await _empDataService.ObtenerInfoSucursalAsync(seguimiento.CLV_SUC);
            var fechasReales = await _empDataService.ObtenerFechasRealesAsync(seguimiento.CLV_SUC, PeriodoDef);

            var vm = new ObservacionVM
            {
                ID = seguimiento.ID,
                CLV_SUC = seguimiento.CLV_SUC,
                SUCURSAL = sucInfo?.Nombre ?? seguimiento.CLV_SUC,
                RUTA = sucInfo?.RUTA ?? 0,
                REGION_NOMBRE = (sucInfo?.REGION ?? 0) switch
                {
                    11 => "Central",
                    12 => "México",
                    13 => "Norte",
                    14 => "Sureste",
                    _ => "Sin región"
                },
                REGION = sucInfo?.REGION ?? 0,
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
            ModelState.Remove(nameof(ObservacionVM.FECHA_INI_RE));
            ModelState.Remove(nameof(ObservacionVM.FECHA_FIN_RE));

            if (!ModelState.IsValid)
            {
                var sucInfo = await _empDataService.ObtenerInfoSucursalAsync(model.CLV_SUC);
                var fechasReales = await _empDataService.ObtenerFechasRealesAsync(model.CLV_SUC, PeriodoDef);
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
                _logger.LogError(ex, "Error al guardar observación para ID={ID}.", model.ID);
                TempData["Mensaje"] = "Error al guardar. Intente de nuevo.";
                TempData["TipoAlerta"] = "danger";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /Seguimiento/Importar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar()
        {
            const string sql = @"
                INSERT INTO mttos.dbo.Seguimientos (CLV_SUC)
                SELECT suc.CLV_SUC
                FROM   Iker.dbo.Sucursales AS suc
                WHERE  suc.ACTIVO = 1
                  AND  NOT EXISTS (
                      SELECT 1
                      FROM   mttos.dbo.Seguimientos AS s
                      WHERE  s.CLV_SUC = suc.CLV_SUC
                  );";
            try
            {
                int insertados = await _context.Database.ExecuteSqlRawAsync(sql);
                TempData["Mensaje"] = $"¡Importación exitosa! Se agregaron {insertados} sucursales.";
                TempData["TipoAlerta"] = "success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al importar sucursales.");
                TempData["Mensaje"] = "Error al importar sucursales.";
                TempData["TipoAlerta"] = "danger";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET  /Seguimiento/Exportar
        [HttpGet]
        public async Task<IActionResult> Exportar(
            int? filtroRuta,
            int? filtroRegion,
            int? filtroMes,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodo = filtroPeriodo ?? PeriodoDef;

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta, filtroRegion, filtroMes, ocultarSinFecha, periodo);

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");

            hoja.Style.Font.FontName = "Arial";
            hoja.Style.Font.FontSize = 10;

            // Encabezados
            hoja.Cell("A3").Value = "Ruta";
            hoja.Range("A3:A4").Merge();

            hoja.Cell("B3").Value = "Región";
            hoja.Range("B3:B4").Merge();

            hoja.Cell("C3").Value = "Centro de Ventas";
            hoja.Range("C3:C4").Merge();

            hoja.Cell("D3").Value = "Fecha Estimada";
            hoja.Range("D3:E3").Merge();

            hoja.Cell("F3").Value = "Fecha Real";
            hoja.Range("F3:G3").Merge();

            hoja.Cell("H3").Value = "Observaciones";
            hoja.Range("H3:H4").Merge();

            hoja.Cell("D4").Value = "Inicio";
            hoja.Cell("E4").Value = "Fin";
            hoja.Cell("F4").Value = "Inicio";
            hoja.Cell("G4").Value = "Fin";

            var rango = hoja.Range("A3:H4");
            rango.Style
                 .Font.SetBold(true)
                 .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                 .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            rango.Cells().Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
            rango.Cells().Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            // Datos 
            int fila = 5;
            foreach (var d in datos)
            {
                hoja.Cell(fila, "A").Value = d.RUTA;
                hoja.Cell(fila, "B").Value = d.REGION;
                hoja.Cell(fila, "C").Value = d.SUCURSAL;
                hoja.Cell(fila, "D").Value = FormatFechaExcel(d.FECHA_INI_ES);
                hoja.Cell(fila, "E").Value = FormatFechaExcel(d.FECHA_FIN_ES);
                hoja.Cell(fila, "F").Value = FormatFechaExcel(d.FECHA_INI_RE);
                hoja.Cell(fila, "G").Value = FormatFechaExcel(d.FECHA_FIN_RE);
                hoja.Cell(fila, "H").Value = d.OBSERVACIONES ?? string.Empty;

                hoja.Range(fila, 1, fila, 8).Style
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetInsideBorder(XLBorderStyleValues.Thin);
                hoja.Range(fila, 4, fila, 7).Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                fila++;
            }

            hoja.Columns("A:H").AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Mantenimientos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // Carga dinamica de sucursales para el filtro del Index
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursalesFiltro(int ruta)
        {
            var sucursales = await _empDataService.ObtenerSucursalesPorRutaAsync(ruta);
            return Json(sucursales.Select(s => new { value = s.CLV_SUC, text = s.Nombre }));
        }

        private static string FormatFechaExcel(DateTime? fecha) =>
            fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy") : string.Empty;
    }
}