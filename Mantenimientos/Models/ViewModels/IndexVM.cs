using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mantenimientos.Models.ViewModels
{
    public class IndexVM
    {
        // ── Resultados ────────────────────────────────────────────────────────
        public List<SeguimientoViewModel> Seguimientos { get; set; } = new();

        // ── Periodo activo (para mostrar badge informativo en la vista) ───────
        public int PeriodoActual { get; set; }

        // ── Filtros ───────────────────────────────────────────────────────────
        public int? FiltroRuta { get; set; }
        public int? FiltroMes { get; set; }

        /// <summary>Periodo seleccionado por el usuario. Por defecto = PeriodoActual.</summary>
        public int FiltroPeriodo { get; set; }

        // ── Listas para los dropdowns ─────────────────────────────────────────
        public List<SelectListItem> RutasDisponibles { get; set; } = new();
        public List<SelectListItem> MesesDisponibles { get; set; } = new();

        /// <summary>Solo contiene dos opciones: periodo actual y periodo anterior.</summary>
        public List<SelectListItem> PeriodosDisponibles { get; set; } = new();
    }

    /// <summary>
    /// Resultado aplanado de Seguimientos + JOIN Sucursales + JOIN DBICET.
    /// </summary>
    public class SeguimientoViewModel
    {
        public int ID { get; set; }
        public string CLV_SUC { get; set; } = string.Empty;
        public int ID_PERIODO { get; set; }
        public string SUCURSAL { get; set; } = string.Empty;
        public int RUTA { get; set; }
        public int REGION { get; set; }
        public string REGION_NOMBRE { get; set; } = string.Empty;
        public DateTime? FECHA_INI_ES { get; set; }
        public DateTime? FECHA_FIN_ES { get; set; }
        public DateTime? FECHA_INI_RE { get; set; }   // De DBICET — solo lectura
        public DateTime? FECHA_FIN_RE { get; set; }   // De DBICET — solo lectura
        public string? OBSERVACIONES { get; set; }
    }
}
