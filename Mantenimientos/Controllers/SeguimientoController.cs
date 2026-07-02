using ClosedXML.Excel;
using Mantenimientos.Data;
using Mantenimientos.Models;
using Mantenimientos.Models.ViewModels;
using Mantenimientos.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
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
            string? filtroEmpresa,
            int? filtroMes,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            // Si el usuario no eligió periodo, se usa el actual.
            int periodoActual = await _periodoService.ObtenerPeriodoActualAsync();
            int periodoActivo = filtroPeriodo ?? periodoActual;

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                periodo: periodoActivo,
                filtroRuta: filtroRuta,
                filtroEmpresa: filtroEmpresa,
                filtroMes: filtroMes,
                ocultarSinFecha: ocultarSinFecha);

            // datos para filtros 
            var listaRutas = await _empDataService.ObtenerRutasAsync();
            var listaPeriodos = await _periodoService.ObtenerPeriodosDisponiblesAsync();

            var meses = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = new DateTime(2000, m, 1).ToString("MMMM"),
                    Selected = filtroMes.HasValue && filtroMes.Value == m
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
                FiltroMes = filtroMes,

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

                // Solo periodo actual y anterior
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

            // Fechas reales
            var fechasReales = await _empDataService.ObtenerFechasRealesAsync(
                seguimiento.CLV_SUC,
                seguimiento.ID_PERIODO);

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
            // Campos que vienen del servidor, no del form
            ModelState.Remove(nameof(ObservacionVM.SUCURSAL));
            ModelState.Remove(nameof(ObservacionVM.RUTA));
            ModelState.Remove(nameof(ObservacionVM.REGION));
            ModelState.Remove(nameof(ObservacionVM.REGION_NOMBRE));
            ModelState.Remove(nameof(ObservacionVM.FECHA_INI_RE));
            ModelState.Remove(nameof(ObservacionVM.FECHA_FIN_RE));

            if (!ModelState.IsValid)
            {
                // Recargar datos de solo lectura para volver a mostrar la vista
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

                // ID_PERIODO no se modifica
                existente.FECHA_INI_ES = model.FECHA_INI_ES;
                existente.FECHA_FIN_ES = model.FECHA_FIN_ES;
                existente.OBSERVACIONES = model.OBSERVACIONES;

                _context.Seguimientos.Update(existente);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Registro actualizado con éxito.";
                TempData["TipoAlerta"] = "success";
            }
            catch (Exception)
            {
                TempData["Mensaje"] = "Error al guardar. Intente de nuevo.";
                TempData["TipoAlerta"] = "danger";
            }

            // Retornar al Index con los filtros que se tenían activos
            return RedirectToAction(nameof(Index),
                new { filtroPeriodo = model.ID_PERIODO });
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
                    sql,
                    new SqlParameter("@PeriodoActual", periodoActual));

                TempData["Mensaje"] = $"Importación exitosa — {insertados} sucursales agregadas para el Periodo {periodoActual}.";
                TempData["TipoAlerta"] = "success";
            }
            catch (Exception)
            {
                TempData["Mensaje"] = "Error al importar sucursales. Revisa los logs.";
                TempData["TipoAlerta"] = "danger";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET  /Seguimiento/Exportar
        [HttpGet]
        public async Task<IActionResult> Exportar(
            int? filtroRuta,
            string? filtroEmpresa,
            int? filtroMes,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodoActual = await _periodoService.ObtenerPeriodoActualAsync();
            int periodo = filtroPeriodo ?? periodoActual;

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                periodo: periodo,
                filtroRuta: filtroRuta,
                filtroEmpresa: filtroEmpresa,
                filtroMes: filtroMes,
                ocultarSinFecha: ocultarSinFecha);

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");

            hoja.Style.Font.FontName = "Arial";
            hoja.Style.Font.FontSize = 10;

            // Encabezados
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
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Fechas_P{periodo}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // AJAX  GET /Seguimiento/ObtenerSucursalesFiltro?ruta=X
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursalesFiltro(int ruta)
        {
            var suc = await _empDataService.ObtenerSucursalesPorRutaAsync(ruta);
            return Json(suc.Select(s => new { value = s.CLV_SUC, text = s.Nombre }));
        }

        // Helper
        private static string FormatFechaExcel(DateTime? f) =>
            f.HasValue ? f.Value.ToString("dd/MM/yyyy") : string.Empty;
    }
}
