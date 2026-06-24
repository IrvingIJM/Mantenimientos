using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mantenimientos.Models.ViewModels
{
    public class IndexVM
    {
        // ── Resultados ─────────────────────────────────────────────────────────
        public List<SeguimientoViewModel> Seguimientos { get; set; } = new();

        // ── Filtros ────────────────────────────────────────────────────────────
        public int? FiltroRuta { get; set; }
        public int? FiltroRegion { get; set; }   // NUEVO
        public int? FiltroMes { get; set; }
        public int? FiltroAnio { get; set; }
        public int? FiltroPeriodo { get; set; }   // NUEVO
        public string? FiltroSucursal { get; set; }   // Valor = CLV_SUC

        // ── Listas para los dropdowns ──────────────────────────────────────────
        public List<SelectListItem> RutasDisponibles { get; set; } = new();
        public List<SelectListItem> SucursalesDisponibles { get; set; } = new();
        public List<SelectListItem> MesesDisponibles { get; set; } = new();
        public List<SelectListItem> AniosDisponibles { get; set; } = new();
        public List<SelectListItem> RegionesDisponibles { get; set; } = new();  // NUEVO
        public List<SelectListItem> PeriodosDisponibles { get; set; } = new();  // NUEVO
    }

    /// <summary>
    /// Resultado aplanado de Seguimientos + JOIN Sucursales + JOIN DBICET.
    /// </summary>
    public class SeguimientoViewModel
    {
        public int ID { get; set; }
        public string CLV_SUC { get; set; } = string.Empty;
        public string NOMBRE_SUCURSAL { get; set; } = string.Empty;
        public int RUTA { get; set; }
        public int REGION { get; set; }
        public DateTime? FECHA_INI_ES { get; set; }
        public DateTime? FECHA_FIN_ES { get; set; }
        public DateTime? FECHA_INI_RE { get; set; }   // De DBICET (solo lectura)
        public DateTime? FECHA_FIN_RE { get; set; }   // De DBICET (solo lectura)
        public int? DIAS_ATRASO { get; set; }
        public string? OBSERVACIONES { get; set; }
    }
}
