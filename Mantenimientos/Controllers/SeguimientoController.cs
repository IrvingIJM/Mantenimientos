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

        // ══════════════════════════════════════════════════════════════════════
        // GET  /Seguimiento/Index
        // ══════════════════════════════════════════════════════════════════════
        public async Task<IActionResult> Index(
            int? filtroRuta,
            int? filtroRegion,
            string? filtroSucursal,   // CLV_SUC
            int? filtroMes,
            int? filtroAnio,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodo = filtroPeriodo ?? PeriodoDefault;

            // ── Sincronizar DIAS_ATRASO con el periodo seleccionado ────────────
            await SincronizarDiasAtrasoAsync(periodo);

            // ── Obtener datos con JOINs ───────────────────────────────────────
            var datos = await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta,
                filtroRegion,
                filtroMes,
                filtroAnio,
                filtroSucursal,
                ocultarSinFecha,
                periodo);

            var seguimientos = datos.Select(d => new SeguimientoViewModel
            {
                ID = d.ID,
                CLV_SUC = d.CLV_SUC,
                NOMBRE_SUCURSAL = d.NOMBRE_SUCURSAL,
                RUTA = d.RUTA,
                REGION = d.REGION,
                FECHA_INI_ES = d.FECHA_INI_ES,
                FECHA_FIN_ES = d.FECHA_FIN_ES,
                FECHA_INI_RE = d.FECHA_INI_RE,
                FECHA_FIN_RE = d.FECHA_FIN_RE,
                DIAS_ATRASO = d.DIAS_ATRASO,
                OBSERVACIONES = d.OBSERVACIONES
            }).ToList();

            // ── Catálogos para los filtros ────────────────────────────────────
            var rutas = await _empDataService.ObtenerRutasAsync();
            var regiones = await _empDataService.ObtenerRegionesAsync();
            var periodos = await _empDataService.ObtenerPeriodosAsync();

            // Sucursales del filtro de ruta seleccionada (o todas si no hay ruta)
            List<SucursalDto> sucursalesFiltro;
            if (filtroRuta.HasValue)
                sucursalesFiltro = await _empDataService.ObtenerSucursalesPorRutaAsync(filtroRuta.Value);
            else
                sucursalesFiltro = datos
                    .Select(d => new SucursalDto { CLV_SUC = d.CLV_SUC, Nombre = d.NOMBRE_SUCURSAL })
                    .DistinctBy(s => s.CLV_SUC)
                    .OrderBy(s => s.Nombre)
                    .ToList();

            var meses = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = new DateTime(2000, m, 1).ToString("MMMM"),
                    Selected = filtroMes.HasValue && filtroMes.Value == m
                }).ToList();

            var anios = Enumerable.Range(2023, (DateTime.Now.Year - 2023) + 2)
                .OrderByDescending(a => a)
                .Select(a => new SelectListItem
                {
                    Value = a.ToString(),
                    Text = a.ToString(),
                    Selected = filtroAnio.HasValue && filtroAnio.Value == a
                }).ToList();

            ViewBag.OcultarSinFecha = ocultarSinFecha;

            var vm = new IndexVM
            {
                Seguimientos = seguimientos,
                FiltroRuta = filtroRuta,
                FiltroRegion = filtroRegion,
                FiltroSucursal = filtroSucursal,
                FiltroMes = filtroMes,
                FiltroAnio = filtroAnio,
                FiltroPeriodo = filtroPeriodo,

                RutasDisponibles = rutas
                    .Select(r => new SelectListItem
                    {
                        Value = r.ToString(),
                        Text = r.ToString(),
                        Selected = filtroRuta.HasValue && filtroRuta.Value == r
                    }).ToList(),

                RegionesDisponibles = regiones
                    .Select(r => new SelectListItem
                    {
                        Value = r.ToString(),
                        Text = $"Región {r}",
                        Selected = filtroRegion.HasValue && filtroRegion.Value == r
                    }).ToList(),

                SucursalesDisponibles = sucursalesFiltro
                    .Select(s => new SelectListItem
                    {
                        Value = s.CLV_SUC,
                        Text = s.Nombre,
                        Selected = s.CLV_SUC == filtroSucursal
                    }).ToList(),

                MesesDisponibles = meses,
                AniosDisponibles = anios,

                PeriodosDisponibles = periodos
                    .Select(p => new SelectListItem
                    {
                        Value = p.ToString(),
                        Text = $"Periodo {p}",
                        Selected = p == periodo
                    }).ToList()
            };

            return View(vm);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET  /Seguimiento/Observacion/{id}
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Observacion(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
                return RedirectToAction(nameof(Index));

            var seguimiento = await _context.Seguimientos.FindAsync(id.Value);
            if (seguimiento == null)
                return NotFound();

            // Datos del JOIN con Sucursales
            var sucInfo = await _empDataService.ObtenerInfoSucursalAsync(seguimiento.CLV_SUC);

            // Fechas reales del periodo por defecto
            var fechasReales = await _empDataService.ObtenerFechasRealesAsync(
                seguimiento.CLV_SUC, PeriodoDefault);

            var vm = new ObservacionVM
            {
                ID = seguimiento.ID,
                CLV_SUC = seguimiento.CLV_SUC,
                NOMBRE_SUCURSAL = sucInfo?.Nombre ?? seguimiento.CLV_SUC,
                RUTA = sucInfo?.RUTA ?? 0,
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

        // ══════════════════════════════════════════════════════════════════════
        // POST /Seguimiento/Observacion
        // Solo se guardan: FECHA_INI_ES, FECHA_FIN_ES, OBSERVACIONES, DIAS_ATRASO
        // Las fechas reales NO se guardan en Seguimientos (vienen de DBICET).
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Observacion(ObservacionVM model)
        {
            // Quitar validaciones de campos que no se envían en el POST
            ModelState.Remove(nameof(ObservacionVM.NOMBRE_SUCURSAL));
            ModelState.Remove(nameof(ObservacionVM.RUTA));
            ModelState.Remove(nameof(ObservacionVM.REGION));
            ModelState.Remove(nameof(ObservacionVM.FECHA_INI_RE));
            ModelState.Remove(nameof(ObservacionVM.FECHA_FIN_RE));
            ModelState.Remove(nameof(ObservacionVM.DIAS_ATRASO));

            if (!ModelState.IsValid)
            {
                // Recargar datos de solo lectura para re-mostrar la vista
                var sucInfo = await _empDataService.ObtenerInfoSucursalAsync(model.CLV_SUC);
                var fechasReales = await _empDataService.ObtenerFechasRealesAsync(model.CLV_SUC, PeriodoDefault);
                model.NOMBRE_SUCURSAL = sucInfo?.Nombre ?? model.CLV_SUC;
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

                // Obtener fechas reales para recalcular DIAS_ATRASO
                var fechasReales = await _empDataService.ObtenerFechasRealesAsync(
                    existente.CLV_SUC, PeriodoDefault);

                int diasAtraso = 0;
                if (fechasReales?.FechaInicio != null && model.FECHA_INI_ES.HasValue)
                {
                    // Positivo = atraso (real inició después de lo estimado)
                    // Negativo = adelanto
                    diasAtraso = (int)(fechasReales.FechaInicio.Value - model.FECHA_INI_ES.Value)
                                      .TotalDays;
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

        // ══════════════════════════════════════════════════════════════════════
        // POST /Seguimiento/Importar
        // Importa sucursales activas de Iker que aún no existen en mttos.
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar()
        {
            // Usa CLV_SUC como clave de deduplicación
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

        // ══════════════════════════════════════════════════════════════════════
        // GET  /Seguimiento/Exportar  — Exportación a Excel con los filtros activos
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Exportar(
            int? filtroRuta,
            int? filtroRegion,
            string? filtroSucursal,
            int? filtroMes,
            int? filtroAnio,
            int? filtroPeriodo,
            bool ocultarSinFecha = false)
        {
            int periodo = filtroPeriodo ?? PeriodoDefault;

            await SincronizarDiasAtrasoAsync(periodo);

            var datos = await _empDataService.ObtenerSeguimientosAsync(
                filtroRuta, filtroRegion,
                filtroMes, filtroAnio,
                filtroSucursal, ocultarSinFecha, periodo);

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");

            hoja.Style.Font.FontName = "Arial";
            hoja.Style.Font.FontSize = 10;

            // ── Encabezados ───────────────────────────────────────────────────
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

            hoja.Cell("H3").Value = "Días Desfasados";
            hoja.Range("H3:H4").Merge();

            hoja.Cell("I3").Value = "Observaciones";
            hoja.Range("I3:I4").Merge();

            hoja.Cell("D4").Value = "Inicio";
            hoja.Cell("E4").Value = "Fin";
            hoja.Cell("F4").Value = "Inicio";
            hoja.Cell("G4").Value = "Fin";

            var rango = hoja.Range("A3:I4");
            rango.Style
                 .Font.SetBold(true)
                 .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                 .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            rango.Cells().Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);
            rango.Cells().Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            // ── Datos ─────────────────────────────────────────────────────────
            int fila = 5;
            foreach (var d in datos)
            {
                hoja.Cell(fila, "A").Value = d.RUTA;
                hoja.Cell(fila, "B").Value = d.REGION;
                hoja.Cell(fila, "C").Value = d.NOMBRE_SUCURSAL;
                hoja.Cell(fila, "D").Value = FormatFechaExcel(d.FECHA_INI_ES);
                hoja.Cell(fila, "E").Value = FormatFechaExcel(d.FECHA_FIN_ES);
                hoja.Cell(fila, "F").Value = FormatFechaExcel(d.FECHA_INI_RE);
                hoja.Cell(fila, "G").Value = FormatFechaExcel(d.FECHA_FIN_RE);
                hoja.Cell(fila, "H").Value = d.DIAS_ATRASO;
                hoja.Cell(fila, "I").Value = d.OBSERVACIONES ?? string.Empty;

                hoja.Range(fila, 1, fila, 9).Style
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetInsideBorder(XLBorderStyleValues.Thin);
                hoja.Range(fila, 4, fila, 8).Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                fila++;
            }

            hoja.Columns("A:I").AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Mantenimientos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // ══════════════════════════════════════════════════════════════════════
        // AJAX — Carga dinámica de sucursales para el filtro del Index
        // GET /Seguimiento/ObtenerSucursalesFiltro?ruta=X
        // ══════════════════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursalesFiltro(int ruta)
        {
            var sucursales = await _empDataService.ObtenerSucursalesPorRutaAsync(ruta);
            return Json(sucursales.Select(s => new { value = s.CLV_SUC, text = s.Nombre }));
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers privados
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Actualiza DIAS_ATRASO en Seguimientos usando las fechas de DBICET
        /// del periodo indicado (por defecto 7).
        /// NOTA: Ya NO actualiza FECHA_INI_RE / FECHA_FIN_RE porque esas columnas
        ///       se eliminaron del esquema; ahora se obtienen en tiempo real vía JOIN.
        /// </summary>
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
                    FROM  Iker.dbo.DBICET
                    WHERE id_periodo = @Periodo
                )
                UPDATE destino
                SET destino.DIAS_ATRASO =
                    CASE
                        WHEN destino.FECHA_INI_ES IS NULL             THEN NULL
                        WHEN origen.F_Inicio IS NULL                  THEN NULL
                        WHEN origen.F_Inicio <= '1900-01-01'          THEN NULL
                        ELSE DATEDIFF(day, destino.FECHA_INI_ES, origen.F_Inicio)
                    END
                FROM  mttos.dbo.Seguimientos AS destino
                INNER JOIN UltimosMovimientos AS origen
                       ON  destino.CLV_SUC = origen.CLV_SUC
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
