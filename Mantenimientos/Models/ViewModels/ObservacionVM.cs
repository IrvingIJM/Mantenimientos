using System.ComponentModel.DataAnnotations;

namespace Mantenimientos.Models.ViewModels
{
    public class ObservacionVM
    {
        // ── Identificador ──────────────────────────────────────────────────────
        public int ID { get; set; }

        /// <summary>Clave de sucursal — se envía como campo oculto.</summary>
        [Required]
        public string CLV_SUC { get; set; } = string.Empty;

        // ── Datos de solo lectura (vienen del JOIN con Iker.dbo.Sucursales) ───
        [Display(Name = "Ruta")]
        public int RUTA { get; set; }

        [Display(Name = "Sucursal")]
        public string SUCURSAL { get; set; } = string.Empty;

        [Display(Name = "Región")]
        public int REGION { get; set; }

        // ── Fechas estimadas (editables) ───────────────────────────────────────
        [Display(Name = "Fecha Inicio Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_ES { get; set; }

        [Display(Name = "Fecha Fin Estimada")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_ES { get; set; }

        // ── Fechas reales (solo lectura — vienen de Iker.dbo.DBICET) ──────────
        [Display(Name = "Fecha Inicio Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_INI_RE { get; set; }

        [Display(Name = "Fecha Fin Real")]
        [DataType(DataType.Date)]
        public DateTime? FECHA_FIN_RE { get; set; }

        // ── Días de atraso / adelanto (calculado en el servidor) ───────────────
        [Display(Name = "Días Atraso/Adelanto")]
        public int? DIAS_ATRASO { get; set; }

        // ── Observaciones (máx. 200 palabras) ─────────────────────────────────
        [Display(Name = "Observaciones")]
        [DataType(DataType.MultilineText)]
        public string? OBSERVACIONES { get; set; }
    }
}
