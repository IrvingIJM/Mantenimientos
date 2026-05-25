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

        //Modulo de consulta
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
                .Select(s => s.RUTA)
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
            };

            return View(viewModel);
        }

        //Otros metodos del controlador
    }
}