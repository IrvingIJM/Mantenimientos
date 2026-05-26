using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mantenimientos.Models.ViewModels
{
    public class IndexVM
    {
        //Lista de resultados
        public List<SeguimientoViewModel> Seguimientos { get; set; } = new();

        //Fltros
        public int? FiltroRuta { get; set; }
        public int? FiltroMes { get; set; }
        public int? FiltroAnio { get; set; }
        public string? FiltroSucursal { get; set; }

        public List<SelectListItem> RutasDisponibles { get; set; } = new();
        public List<SelectListItem> SucursalesDisponibles { get; set; } = new();
        public List<SelectListItem> MesesDisponibles { get; set; } = new();
    }

    public class SeguimientoViewModel
    {
        public int ID { get; set; }
        public int RUTA { get; set; }
        public string SUCURSAL { get; set; } = string.Empty;
        public DateTime FECHA_INI_ES { get; set; }
        public DateTime FECHA_FIN_ES { get; set; }
        public DateTime FECHA_INI_RE { get; set; }
        public DateTime FECHA_FIN_RE { get; set; }
        public int DIAS_ATRASO { get; set; }
        public string? OBSERVACIONES { get; set; }
    }
}
