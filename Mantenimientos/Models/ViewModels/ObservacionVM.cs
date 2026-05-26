using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mantenimientos.Models.ViewModels
{
    public class ObservacionVM
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "La ruta es obligatoria.")]
        [Display(Name = "Ruta")]
        public int RUTA { get; set; }

        [Required(ErrorMessage = "La sucursal es obligatoria.")]
        [Display(Name = "Sucursal")]
        public string SUCURSAL { get; set; } = string.Empty;

        [Display(Name = "Fecha Inicio Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_ES { get; set; }

        [Display(Name = "Fecha Fin Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_ES { get; set; }

        //Datos de entrada
        [Display(Name = "Fecha Inicio Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_RE { get; set; }

        [Display(Name = "Fecha Fin Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_RE { get; set; }

        [Display(Name = "Observaciones")]
        [DataType(DataType.MultilineText)]
        public string? OBSERVACIONES { get; set; }

        //Listas para los dropdown
        public List<SelectListItem> RutasDisponibles { get; set; } = new();
        public List<SelectListItem> SucursalesDisponibles { get; set; } = new();
    }
}
