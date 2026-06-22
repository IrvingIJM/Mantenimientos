using System.ComponentModel.DataAnnotations;

namespace Mantenimientos.Models.ViewModels
{
    public class ObservacionVM
    {
        public int ID { get; set; }

        [Required]
        public int CLV_SUC { get; set; }

        // Solo lecturam, vienen del JOIN
        [Display(Name = "Ruta")]
        public int RUTA { get; set; }

        [Display(Name = "Sucursal")]
        public string SUCURSAL { get; set; } = string.Empty;

        [Display(Name = "Región")]
        public string REGION { get; set; } = string.Empty;

        // fechas reales
        [Display(Name = "Fecha Inicio Real")]
        public DateTime? FECHA_INI_RE { get; set; }

        [Display(Name = "Fecha Fin Real")]
        public DateTime? FECHA_FIN_RE { get; set; }

        // dias calculados
        [Display(Name = "Días de Atraso / Adelanto")]
        public int? DIAS_ATRASO { get; set; }

        // registros editables
        [Display(Name = "Fecha Inicio Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_ES { get; set; }

        [Display(Name = "Fecha Fin Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_ES { get; set; }

        [Display(Name = "Observaciones")]
        [DataType(DataType.MultilineText)]
        public string? OBSERVACIONES { get; set; }
    }
}