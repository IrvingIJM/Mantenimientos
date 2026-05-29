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
        public int Ruta
        {
            get => RUTA;
            set => RUTA = value;
        }

        [Required(ErrorMessage = "La sucursal es obligatoria.")]
        [Display(Name = "Sucursal")]
        public string SUCURSAL { get; set; } = string.Empty;
        public string Sucursal
        {
            get => SUCURSAL;
            set => SUCURSAL = value;
        }

        [Display(Name = "Fecha Inicio Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_ES { get; set; }
        public DateTime? FechaInicioEstimada
        {
            get => FECHA_INI_ES;
            set => FECHA_INI_ES = value;
        }

        [Display(Name = "Fecha Fin Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_ES { get; set; }
        public DateTime? FechaFinEstimada
        {
            get => FECHA_FIN_ES;
            set => FECHA_FIN_ES = value;
        }

        //Datos de entrada
        [Display(Name = "Fecha Inicio Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_RE { get; set; }
        public DateTime? FechaInicioReal
        {
            get => FECHA_INI_RE;
            set => FECHA_INI_RE = value;
        }

        [Display(Name = "Fecha Fin Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_RE { get; set; }
        public DateTime? FechaFinReal
        {
            get => FECHA_FIN_RE;
            set => FECHA_FIN_RE = value;
        }

        [Display(Name = "Observaciones")]
        [DataType(DataType.MultilineText)]
        public string? OBSERVACIONES { get; set; }
        public string? Observaciones
        {
            get => OBSERVACIONES;
            set => OBSERVACIONES = value;
        }

        //Listas para los dropdown
        public List<SelectListItem> RutasDisponibles { get; set; } = new();
        public List<SelectListItem> SucursalesDisponibles { get; set; } = new();
    }
}
