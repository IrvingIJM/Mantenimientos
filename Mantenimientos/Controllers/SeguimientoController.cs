using ClosedXML.Excel;
using Mantenimientos.Data;
using Mantenimientos.Models;
using Mantenimientos.Models.ViewModels;
using Mantenimientos.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Mantenimientos.Controllers
{
    public class SeguimientoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmpDataService _empDataService;
        private readonly ILogger<SeguimientoController> _logger;

        private const int PeriodoDefault = 7;

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
            int? filtroRegion,
            int? filtroMes,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodo = filtroPeriodo ?? PeriodoDefault;

            await SincronizarDiasAtrasoAsync(periodo);

            var datos = (await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta, filtroRegion, filtroMes, ocultarSinFecha, periodo
            )).Select(dto => new SeguimientoViewModel
            {
                ID = dto.ID,
                CLV_SUC = dto.CLV_SUC,
                SUCURSAL = dto.SUCURSAL,
                RUTA = dto.RUTA,
                REGION = dto.REGION,
                FECHA_INI_ES = dto.FECHA_INI_ES,
                FECHA_FIN_ES = dto.FECHA_FIN_ES,
                FECHA_INI_RE = dto.FECHA_INI_RE,
                FECHA_FIN_RE = dto.FECHA_FIN_RE,
                DIAS_ATRASO = dto.DIAS_ATRASO,
                OBSERVACIONES = dto.OBSERVACIONES
            }).ToList();

            var listaRutas = await _empDataService.ObtenerRutasAsync();
            var listaRegiones = await _empDataService.ObtenerRegionesAsync();
            var listaPeriodos = await _empDataService.ObtenerPeriodosAsync();

            var viewModel = new IndexVM
            {
                Seguimientos = datos,

                FiltroRuta = filtroRuta,
                FiltroRegion = filtroRegion,
                FiltroMes = filtroMes,
                FiltroPeriodo = periodo,

                RutasDisponibles = listaRutas.Select(r => new SelectListItem
                {
                    Value = r.ToString(),
                    Text = r.ToString(),
                    Selected = (r == filtroRuta)
                }).ToList(),

                RegionesDisponibles = listaRegiones.Select(r => new SelectListItem
                {
                    Value = r.ToString(),
                    Text = r switch
                    {
                        11 => "Central",
                        12 => "México",
                        13 => "Norte",
                        14 => "Sureste",
                        _ => "Región" + r.ToString()
                    },
                    Selected = (r == filtroRegion)
                }).ToList(),

                PeriodosDisponibles = listaPeriodos.Select(p => new SelectListItem
                {
                    Value = p.ToString(),
                    Text = "Periodo " + p.ToString(),
                    Selected = (p == periodo)
                }).ToList(),

                MesesDisponibles = Enumerable.Range(1, 12).Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = CultureInfo.GetCultureInfo("es-ES").DateTimeFormat.GetMonthName(m),
                    Selected = (m == filtroMes)
                }).ToList()
            };

            ViewBag.OcultarSinFecha = ocultarSinFecha;

            ViewBag.FiltroRuta = filtroRuta;
            ViewBag.FiltroRegion = filtroRegion;
            ViewBag.FiltroMes = filtroMes;
            ViewBag.FiltroPeriodo = periodo;

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

            var fechasReales = await _empDataService.ObtenerFechasRealesAsync(
                seguimiento.CLV_SUC, PeriodoDefault);

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
                DIAS_ATRASO = seguimiento.DIAS_ATRASO,
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
            ModelState.Remove(nameof(ObservacionVM.DIAS_ATRASO));

            if (!ModelState.IsValid)
            {
                var sucInfo = await _empDataService.ObtenerInfoSucursalAsync(model.CLV_SUC);
                var fechasReales = await _empDataService.ObtenerFechasRealesAsync(model.CLV_SUC, PeriodoDefault);
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

                var fechasReales = await _empDataService.ObtenerFechasRealesAsync(
                    existente.CLV_SUC, PeriodoDefault);

                int diasAtraso = 0;
                if (fechasReales?.FechaInicio != null && model.FECHA_INI_ES.HasValue)
                {
                    diasAtraso = (int)(fechasReales.FechaInicio.Value - model.FECHA_INI_ES.Value).TotalDays;
                }

                existente.FECHA_INI_ES = model.FECHA_INI_ES;
                existente.FECHA_FIN_ES = model.FECHA_FIN_ES;
                existente.DIAS_ATRASO = diasAtraso;
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
                FROM Iker.dbo.Sucursales AS suc
                WHERE suc.ACTIVO = 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM mttos.dbo.Seguimientos AS s
                      WHERE s.CLV_SUC = suc.CLV_SUC
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
            int periodo = filtroPeriodo ?? PeriodoDefault;

            await SincronizarDiasAtrasoAsync(periodo);

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta, filtroRegion,
                filtroMes, ocultarSinFecha, periodo);

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");

            hoja.Style.Font.FontName = "Arial";
            hoja.Style.Font.FontSize = 10;

            hoja.Cell("C3").Value = "Centro de Ventas";
            hoja.Range("C3:C4").Merge();

            hoja.Cell("D3").Value = "Fecha Estimada";
            hoja.Range("D3:E3").Merge();

            hoja.Cell("F3").Value = "Fecha Real";
            hoja.Range("F3:G3").Merge();

            hoja.Cell("H3").Value = "Días Desfasados";
            hoja.Range("H3:H4").Merge();

            hoja.Cell("I3").Value = "Observaciones";
            hoja.Range("I3:I4").Merge();

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
                hoja.Cell(fila, "H").Value = d.DIAS_ATRASO;
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
                $"Fechas_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // carga dinamica de sucursales para el filtro
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursalesFiltro(int ruta)
        {
            var sucursales = await _empDataService.ObtenerSucursalesPorRutaAsync(ruta);
            return Json(sucursales.Select(s => new { value = s.CLV_SUC, text = s.Nombre }));
        }

        // Helpers
        private async Task SincronizarDiasAtrasoAsync(int periodo = PeriodoDefault)
        {
            const string sql = @"
                WITH UltimosMovimientos AS (
                    SELECT
                        CLV_SUC,
                        F_Inicio,
                        F_Termino,
                        ROW_NUMBER() OVER (
                            PARTITION BY CLV_SUC
                            ORDER BY F_Inicio DESC
                        ) AS fila
                    FROM Iker.dbo.DBICET
                    WHERE id_periodo = @Periodo
                )
                UPDATE destino
                SET destino.DIAS_ATRASO =
                    CASE
                        WHEN destino.FECHA_INI_ES IS NULL THEN NULL
                        WHEN origen.F_Inicio IS NULL THEN NULL
                        WHEN origen.F_Inicio <= '1900-01-01' THEN NULL
                        ELSE DATEDIFF(day, destino.FECHA_INI_ES, origen.F_Inicio)
                    END
                FROM mttos.dbo.Seguimientos AS destino
                INNER JOIN UltimosMovimientos AS origen
                       ON destino.CLV_SUC = origen.CLV_SUC
                WHERE origen.fila = 1;";
            try
            {
                await _context.Database.ExecuteSqlRawAsync(sql,
                    new Microsoft.Data.SqlClient.SqlParameter("@Periodo", periodo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar DIAS_ATRASO (periodo {P}).", periodo);
            }
        }

        private static string FormatFechaExcel(DateTime? fecha) =>
            fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy") : string.Empty;
    }
}