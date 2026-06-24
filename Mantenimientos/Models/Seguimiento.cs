using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mantenimientos.Models
{
    /// <summary>
    /// Representa la tabla mttos.dbo.Seguimientos.
    /// Los campos RUTA, NOMBRE_SUCURSAL, REGION, FECHA_INI_RE y FECHA_FIN_RE
    /// se obtienen mediante JOINs con Iker.dbo.Sucursales e Iker.dbo.DBICET.
    /// </summary>
    [Table("Seguimientos")]
    public class Seguimiento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        /// <summary>Clave de la sucursal (FK lógica hacia Iker.dbo.Sucursales).</summary>
        [Required]
        [MaxLength(50)]
        public string CLV_SUC { get; set; } = string.Empty;

        [Column(TypeName = "date")]
        public DateTime? FECHA_INI_ES { get; set; }

        [Column(TypeName = "date")]
        public DateTime? FECHA_FIN_ES { get; set; }

        /// <summary>
        /// Calculado: DATEDIFF(day, FECHA_INI_ES, F_Inicio_Real).
        /// Positivo = atraso, negativo = adelanto, 0 = en tiempo.
        /// </summary>
        public int? DIAS_ATRASO { get; set; } = 0;

        [Column(TypeName = "nvarchar(max)")]
        public string? OBSERVACIONES { get; set; }
    }
}
