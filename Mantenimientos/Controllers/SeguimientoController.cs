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
        public async Task<IActionResult> Observacion(ObservacionVM viewModel)
        {
            if (!ModelState.IsValid)
            {
                await CargarDropdownAsync(viewModel);
                return View(viewModel);
            }
            var seguimiento = await _context.Seguimientos
                .FirstOrDefaultAsync(s => s.RUTA == viewModel.RUTA && s.SUCURSAL == viewModel.SUCURSAL);
            if (seguimiento == null)
            {
                seguimiento = new Seguimiento
                {
                    RUTA = viewModel.RUTA,
                    SUCURSAL = viewModel.SUCURSAL,
                    FECHA_INI_ES = viewModel.FECHA_INI_ES ?? FechaDefault,
                    FECHA_FIN_ES = viewModel.FECHA_FIN_ES ?? FechaDefault,
                    FECHA_INI_RE = viewModel.FECHA_INI_RE ?? FechaDefault,
                    FECHA_FIN_RE = viewModel.FECHA_FIN_RE ?? FechaDefault,
                    OBSERVACIONES = viewModel.OBSERVACIONES
                };
                _context.Seguimientos.Add(seguimiento);
            }
            else
            {
                seguimiento.FECHA_INI_ES = viewModel.FECHA_INI_ES ?? FechaDefault;
                seguimiento.FECHA_FIN_ES = viewModel.FECHA_FIN_ES ?? FechaDefault;
                seguimiento.FECHA_INI_RE = viewModel.FECHA_INI_RE ?? FechaDefault;
                seguimiento.FECHA_FIN_RE = viewModel.FECHA_FIN_RE ?? FechaDefault;
                seguimiento.OBSERVACIONES = viewModel.OBSERVACIONES;
                _context.Seguimientos.Update(seguimiento);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}