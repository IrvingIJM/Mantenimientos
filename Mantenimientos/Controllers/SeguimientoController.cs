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
            string? filtroSucursal,
            int? filtroMes,
            int? filtroAnio,
            bool ocultarSinFecha = false)
        {
            string sqlUpdate = @"
                WITH UltimosMovimientos AS (
                    SELECT 
                        SUCURSAL,
                        F_Inicio,
                        F_Termino,
                        ROW_NUMBER() OVER (PARTITION BY SUCURSAL ORDER BY F_Inicio DESC) as fila
                    FROM Iker.dbo.DBICET
                    WHERE id_periodo = 7
                )
                UPDATE destino
                SET 
                    destino.FECHA_INI_RE = CASE WHEN origen.F_Inicio <= '1900-01-01' THEN NULL ELSE origen.F_Inicio END,
                    destino.FECHA_FIN_RE = CASE WHEN origen.F_Termino <= '1900-01-01' THEN NULL ELSE origen.F_Termino END,
                    destino.DIAS_ATRASO = CASE 
                        WHEN destino.FECHA_FIN_ES IS NULL THEN NULL
                        WHEN origen.F_Termino IS NULL OR origen.F_Termino <= '1900-01-01' THEN NULL
                        ELSE DATEDIFF(day, origen.F_Inicio, destino.FECHA_INI_ES)
                    END
                FROM mttos.dbo.Seguimientos AS destino
                INNER JOIN UltimosMovimientos AS origen 
                    ON destino.SUCURSAL = origen.SUCURSAL
                WHERE origen.fila = 1;
            ";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(sqlUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la sincronización automática del Index.");
            }

            var query = _context.Seguimientos.AsQueryable();

            if (filtroRuta.HasValue)
                query = query.Where(s => s.RUTA == filtroRuta.Value);

            if (!string.IsNullOrEmpty(filtroSucursal))
                query = query.Where(s => s.SUCURSAL == filtroSucursal);

            if (filtroMes.HasValue)
                query = query.Where(s => s.FECHA_INI_ES.HasValue && s.FECHA_INI_ES.Value.Month == filtroMes.Value);

            if (filtroAnio.HasValue && filtroAnio.Value > 0)
                query = query.Where(s => s.FECHA_INI_ES.HasValue && s.FECHA_INI_ES.Value.Year == filtroAnio.Value);

            if (ocultarSinFecha)
            {
                query = query.Where(s =>
                    (s.FECHA_INI_RE != null && s.FECHA_INI_RE.Value != null)
                );
            }

            var seguimientos = await query
                .OrderBy(s => s.RUTA)
                .ThenBy(s => s.SUCURSAL)
                .Select(s => new SeguimientoViewModel
                {
                    ID = s.ID,
                    RUTA = s.RUTA,
                    SUCURSAL = s.SUCURSAL,
                    FECHA_INI_ES = s.FECHA_INI_ES,
                    FECHA_FIN_ES = s.FECHA_FIN_ES,
                    FECHA_INI_RE = s.FECHA_INI_RE,
                    FECHA_FIN_RE = s.FECHA_FIN_RE,
                    DIAS_ATRASO = s.DIAS_ATRASO ?? 0,
                    OBSERVACIONES = s.OBSERVACIONES
                })
                .ToListAsync();

            var todasRutas = await _context.Seguimientos
                .Select(r => r.RUTA)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            var sucursalesQuery = _context.Seguimientos.AsQueryable();
            if (filtroRuta.HasValue)
                sucursalesQuery = sucursalesQuery.Where(s => s.RUTA == filtroRuta.Value);

            var todasSucursales = await sucursalesQuery
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

            var aniosDisponibles = Enumerable.Range(2023, (DateTime.Now.Year - 2023) + 1)
                .OrderByDescending(a => a)
                .Select(a => new SelectListItem
                {
                    Value = a.ToString(),
                    Text = a.ToString(),
                    Selected = filtroAnio.HasValue && a == filtroAnio.Value
                })
                .ToList();

            ViewBag.OcultarSinFecha = ocultarSinFecha;

            var viewModel = new IndexVM
            {
                Seguimientos = seguimientos,
                FiltroRuta = filtroRuta,
                FiltroSucursal = filtroSucursal,
                FiltroMes = filtroMes,
                FiltroAnio = filtroAnio,
                RutasDisponibles = todasRutas.Select(r => new SelectListItem { Value = r.ToString(), Text = r.ToString(), Selected = filtroRuta.HasValue && r == filtroRuta.Value }).ToList(),
                SucursalesDisponibles = todasSucursales.Select(s => new SelectListItem { Value = s, Text = s, Selected = s == filtroSucursal }).ToList(),
                MesesDisponibles = meses,
                AniosDisponibles = aniosDisponibles
            };

            return View(viewModel);
        }

        // GET  /Seguimiento/Observacion/{id?}
        [HttpGet]
        public async Task<IActionResult> Observacion(int? id)
        {
            var viewModel = new ObservacionVM();

            if (id.HasValue && id.Value > 0)
            {
                var seguimiento = await _context.Seguimientos.FindAsync(id.Value);
                if (seguimiento == null)
                    return NotFound();

                viewModel = new ObservacionVM
                {
                    ID = seguimiento.ID,
                    RUTA = seguimiento.RUTA,
                    SUCURSAL = seguimiento.SUCURSAL,
                    FECHA_INI_ES = seguimiento.FECHA_INI_ES,
                    FECHA_FIN_ES = seguimiento.FECHA_FIN_ES,
                    FECHA_INI_RE = seguimiento.FECHA_INI_RE,
                    FECHA_FIN_RE = seguimiento.FECHA_FIN_RE,
                    OBSERVACIONES = seguimiento.OBSERVACIONES
                };
            }

            await CargarDropdownAsync(viewModel);
            return View(viewModel);
        }

        // POST /Seguimiento/Importar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar()
        {
            string sqlQuery = @"
                    INSERT INTO mttos.dbo.Seguimientos (RUTA, SUCURSAL)
                    SELECT origen.RUTA, origen.Sucursal 
                    FROM Iker.dbo.Sucursales AS origen
                    WHERE NOT EXISTS (
                        SELECT 1 
                        FROM mttos.dbo.Seguimientos AS destino 
                        WHERE destino.SUCURSAL = origen.SUCURSAL
                    );
                ";

            try
            {
                int registrosInsertados = await _context.Database.ExecuteSqlRawAsync(sqlQuery);
                TempData["Mensaje"] = $"¡Importación exitosa! Se agregaron {registrosInsertados} nuevas sucursales.";
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

        // POST /Seguimiento/Observacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Observacion(ObservacionVM model)
        {
            if (!ModelState.IsValid)
            {
                await CargarDropdownAsync(model);
                return View(model);
            }

            int diasDesfasados = 0;
            if (model.FECHA_FIN_RE.HasValue && model.FECHA_FIN_ES.HasValue)
                diasDesfasados = (int)(model.FECHA_INI_ES.Value - model.FECHA_INI_RE.Value).TotalDays;

            try
            {
                if (model.ID == 0)
                {
                    var nuevo = new Seguimiento
                    {
                        RUTA = model.RUTA,
                        SUCURSAL = model.SUCURSAL,
                        FECHA_INI_ES = model.FECHA_INI_ES,
                        FECHA_FIN_ES = model.FECHA_FIN_ES,
                        FECHA_INI_RE = model.FECHA_INI_RE,
                        FECHA_FIN_RE = model.FECHA_FIN_RE,
                        DIAS_ATRASO = diasDesfasados,
                        OBSERVACIONES = model.OBSERVACIONES?.Trim()
                    };
                    _context.Seguimientos.Add(nuevo);
                }
                else
                {
                    var existente = await _context.Seguimientos.FindAsync(model.ID);
                    if (existente is null) return NotFound();

                    existente.RUTA = model.RUTA;
                    existente.SUCURSAL = model.SUCURSAL;
                    existente.FECHA_INI_ES = model.FECHA_INI_ES;
                    existente.FECHA_FIN_ES = model.FECHA_FIN_ES;
                    existente.FECHA_INI_RE = model.FECHA_INI_RE;
                    existente.FECHA_FIN_RE = model.FECHA_FIN_RE;
                    existente.DIAS_ATRASO = diasDesfasados;
                    existente.OBSERVACIONES = model.OBSERVACIONES?.Trim();

                    _context.Seguimientos.Update(existente);
                }
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Operación realizada con éxito.";
                TempData["TipoAlerta"] = "success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar observación.");
                TempData["Mensaje"] = "Error al guardar.";
                TempData["TipoAlerta"] = "danger";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET  /Seguimiento/Exportar
        [HttpGet]
        public async Task<IActionResult> Exportar(
            int? filtroRuta,
            string? filtroSucursal,
            int? filtroMes,
            int? filtroAnio,
            bool ocultarSinFecha = false)
        {
            string sqlUpdate = @"
                WITH UltimosMovimientos AS (
                    SELECT 
                        SUCURSAL,
                        F_Inicio,
                        F_Termino,
                        ROW_NUMBER() OVER (PARTITION BY SUCURSAL ORDER BY F_Inicio DESC) as fila
                    FROM Iker.dbo.DBICET
                    WHERE id_periodo = 7
                )
                UPDATE destino
                SET 
                    destino.FECHA_INI_RE = CASE WHEN origen.F_Inicio <= '1900-01-01' THEN NULL ELSE origen.F_Inicio END,
                    destino.FECHA_FIN_RE = CASE WHEN origen.F_Termino <= '1900-01-01' THEN NULL ELSE origen.F_Termino END,
                    destino.DIAS_ATRASO = CASE 
                        WHEN destino.FECHA_FIN_ES IS NULL THEN NULL
                        WHEN origen.F_Termino IS NULL OR origen.F_Termino <= '1900-01-01' THEN NULL
                        ELSE DATEDIFF(day, origen.F_Inicio, destino.FECHA_INI_ES)
                    END
                FROM mttos.dbo.Seguimientos AS destino
                INNER JOIN UltimosMovimientos AS origen 
                    ON destino.SUCURSAL = origen.SUCURSAL
                WHERE origen.fila = 1;
            ";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(sqlUpdate);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en actualización previa a Excel."); }

            var query = _context.Seguimientos.AsQueryable();

            if (filtroRuta.HasValue) query = query.Where(s => s.RUTA == filtroRuta.Value);
            if (!string.IsNullOrWhiteSpace(filtroSucursal)) query = query.Where(s => s.SUCURSAL == filtroSucursal);
            if (filtroMes.HasValue) query = query.Where(s => s.FECHA_INI_ES.HasValue && s.FECHA_INI_ES.Value.Month == filtroMes.Value);
            if (filtroAnio.HasValue && filtroAnio.Value > 0) query = query.Where(s => s.FECHA_INI_ES.HasValue && s.FECHA_INI_ES.Value.Year == filtroAnio.Value);

            if (ocultarSinFecha)
            {
                query = query.Where(s =>
                    (s.FECHA_INI_RE != null && s.FECHA_INI_RE.Value != null)
                );
            }

            var datos = await query.OrderBy(s => s.RUTA).ThenBy(s => s.SUCURSAL).ToListAsync();

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Mantenimientos");

            string[] encabezados = { "Ruta", "Sucursal", "F. Inicio Est.", "F. Fin Est.", "F. Inicio Real", "F. Fin Real", "Días Desfasados", "Observaciones" };

            for (int col = 0; col < encabezados.Length; col++)
            {
                var celda = hoja.Cell(4, col + 1);
                celda.Value = encabezados[col];
                celda.Style.Font.SetBold(true).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#457B9D")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(XLColor.White);
            }

            for (int i = 0; i < datos.Count; i++)
            {
                var s = datos[i];
                int row = i + 5;

                hoja.Cell(row, 1).Value = s.RUTA;
                hoja.Cell(row, 2).Value = s.SUCURSAL;
                hoja.Cell(row, 3).Value = FormatFechaExcel(s.FECHA_INI_ES);
                hoja.Cell(row, 4).Value = FormatFechaExcel(s.FECHA_FIN_ES);
                hoja.Cell(row, 5).Value = FormatFechaExcel(s.FECHA_INI_RE);
                hoja.Cell(row, 6).Value = FormatFechaExcel(s.FECHA_FIN_RE);
                hoja.Cell(row, 7).Value = s.DIAS_ATRASO;
                hoja.Cell(row, 8).Value = s.OBSERVACIONES ?? string.Empty;

                var colorFondo = i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#F1FAEE");
                hoja.Range(row, 1, row, 8).Style.Fill.SetBackgroundColor(colorFondo).Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(XLColor.FromHtml("#CCCCCC"));
            }

            using var ms = new System.IO.MemoryStream();
            workbook.SaveAs(ms);
            var content = ms.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Mantenimientos_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // GET  /Seguimiento/ObtenerSucursalesFiltro
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursalesFiltro(int ruta)
        {
            var sucursales = await _context.Seguimientos.Where(s => s.RUTA == ruta).Select(s => s.SUCURSAL).Distinct().OrderBy(s => s).ToListAsync();
            return Json(sucursales);
        }

        // GET  /Seguimiento/ObtenerSucursales
        [HttpGet]
        public async Task<IActionResult> ObtenerSucursales(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta)) return Json(new List<string>());
            var sucursales = await _empDataService.ObtenerSucursales(ruta);
            return Json(sucursales);
        }

        // GET  /Seguimiento/ObtenerFechasEstimadas
        [HttpGet]
        public async Task<IActionResult> ObtenerFechasEstimadas(string ruta, string sucursal)
        {
            if (string.IsNullOrWhiteSpace(ruta) || string.IsNullOrWhiteSpace(sucursal)) return Json(new { });
            var fechas = await _empDataService.ObtenerFechasEstimadasAsync(ruta, sucursal);
            if (fechas is null) return Json(new { });
            return Json(new { fechaIniEst = fechas.FechaInicioEstimada.ToString("yyyy-MM-dd"), fechaFinEst = fechas.FechaFinEstimada.ToString("yyyy-MM-dd") });
        }

        private async Task CargarDropdownAsync(ObservacionVM model)
        {
            try
            {
                var rutas = await _empDataService.ObtenerRutasAsync();
                model.RutasDisponibles = rutas.Select(r => new SelectListItem { Value = r, Text = r }).ToList();
                if (model.RUTA != 0)
                {
                    var sucursales = await _empDataService.ObtenerSucursales(model.RUTA.ToString());
                    model.SucursalesDisponibles = sucursales.Select(s => new SelectListItem { Value = s, Text = s }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dropdowns.");
                model.RutasDisponibles = new List<SelectListItem>();
                model.SucursalesDisponibles = new List<SelectListItem>();
            }
        }
        private static string FormatFechaExcel(DateTime? fecha) => fecha?.ToString("dd/MM/yyyy") ?? "";
    }
}