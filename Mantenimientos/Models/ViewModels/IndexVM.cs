using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mantenimientos.Models.ViewModels
{
    public class IndexVM
    {
        // Lista de resultados
        public List<SeguimientoViewModel> Seguimientos { get; set; } = new();

        // Filtros
        public int? FiltroRuta { get; set; }
        public string? FiltroRegion { get; set; }
        public string? FiltroClvSuc { get; set; }
        public int? FiltroMes { get; set; }
        public int? FiltroAnio { get; set; }
        public int? FiltroPeriodo { get; set; }

        // Listas para dropdowns
        public List<SelectListItem> RutasDisponibles { get; set; } = new();
        public List<SelectListItem> RegionesDisponibles { get; set; } = new();
        public List<SelectListItem> MesesDisponibles { get; set; } = new();
        public List<SelectListItem> PeriodosDisponibles { get; set; } = new();
    }

    public class SeguimientoViewModel
    {
        public int ID { get; set; }
        public int CLV_SUC { get; set; }

        //Mediante join
        public int RUTA { get; set; }
        public string SUCURSAL { get; set; } = string.Empty;
        public string REGION { get; set; } = string.Empty;

        // Desde tabla Seguimientos
        public DateTime? FECHA_INI_ES { get; set; }
        public DateTime? FECHA_FIN_ES { get; set; }

        // JOIN DBICET
        public DateTime? FECHA_INI_RE { get; set; }
        public DateTime? FECHA_FIN_RE { get; set; }

        public int DIAS_ATRASO { get; set; }

        public string? OBSERVACIONES { get; set; }
    }
}