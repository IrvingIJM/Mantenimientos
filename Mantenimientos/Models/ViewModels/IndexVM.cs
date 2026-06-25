using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mantenimientos.Models.ViewModels
{
    public class IndexVM
    {
        // ── Resultados ─────────────────────────────────────────────────────────
        public List<SeguimientoViewModel> Seguimientos { get; set; } = new();

        // ── Filtros ────────────────────────────────────────────────────────────
        public int? FiltroRuta { get; set; }
        public int? FiltroRegion { get; set; }
        public int? FiltroMes { get; set; }
        public int? FiltroPeriodo { get; set; }

        // ── Listas para los dropdowns ──────────────────────────────────────────
        public List<SelectListItem> RutasDisponibles { get; set; } = new();
        public List<SelectListItem> MesesDisponibles { get; set; } = new();
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
        public string SUCURSAL { get; set; } = string.Empty;
        public int RUTA { get; set; }
        public int REGION { get; set; }
        public string REGION_NOMBRE { get; set; } = string.Empty;
        public DateTime? FECHA_INI_ES { get; set; }
        public DateTime? FECHA_FIN_ES { get; set; }
        public DateTime? FECHA_INI_RE { get; set; }
        public DateTime? FECHA_FIN_RE { get; set; }
        public int? DIAS_ATRASO { get; set; }
        public string? OBSERVACIONES { get; set; }
    }
}
