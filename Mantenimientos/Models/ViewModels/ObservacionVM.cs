using System.ComponentModel.DataAnnotations;

namespace Mantenimientos.Models.ViewModels
{
    public class ObservacionVM
    {
        // ── Identificadores ───────────────────────────────────────────────────
        public int ID { get; set; }

        [Required]
        public string CLV_SUC { get; set; } = string.Empty;

        /// <summary>
        /// Periodo de este registro. Se muestra como etiqueta (no editable)
        /// y se conserva en un campo oculto para que el POST lo reciba.
        /// </summary>
        public int ID_PERIODO { get; set; }

        // ── Datos de solo lectura (JOIN con Iker.dbo.Sucursales) ─────────────
        [Display(Name = "Ruta")]
        public int RUTA { get; set; }

        [Display(Name = "Sucursal")]
        public string SUCURSAL { get; set; } = string.Empty;

        [Display(Name = "Región")]
        public int REGION { get; set; }

        public string REGION_NOMBRE { get; set; } = string.Empty;

        // ── Fechas estimadas (editables) ──────────────────────────────────────
        [Display(Name = "Fecha Inicio Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_ES { get; set; }

        [Display(Name = "Fecha Fin Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_ES { get; set; }

        // ── Fechas reales (solo lectura — Iker.dbo.DBICET del ID_PERIODO) ─────
        [Display(Name = "Fecha Inicio Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_RE { get; set; }

        [Display(Name = "Fecha Fin Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_RE { get; set; }

        // ── Observaciones ─────────────────────────────────────────────────────
        [Display(Name = "Observaciones")]
        [DataType(DataType.MultilineText)]
        public string? OBSERVACIONES { get; set; }
    }
}
