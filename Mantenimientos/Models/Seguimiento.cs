using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mantenimientos.Models
{
    /// <summary>
    /// Tabla mttos.dbo.Seguimientos.
    /// Cada fila es el seguimiento de UNA sucursal (CLV_SUC) para UN periodo (ID_PERIODO).
    /// La combinación CLV_SUC + ID_PERIODO es única.
    ///
    /// Campos que NO están aquí y se obtienen vía JOIN:
    ///   - Nombre de la sucursal, RUTA, REGION  → Iker.dbo.Sucursales
    ///   - FECHA_INI_RE, FECHA_FIN_RE           → Iker.dbo.DBICET (periodo = ID_PERIODO)
    /// </summary>
    [Table("Seguimientos")]
    public class Seguimiento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        /// <summary>Clave de sucursal — FK lógica a Iker.dbo.Sucursales.CLV_SUC.</summary>
        [Required]
        [MaxLength(50)]
        public string CLV_SUC { get; set; } = string.Empty;

        /// <summary>
        /// Periodo al que pertenece este seguimiento.
        /// Se asigna en el momento de la importación (Importar action).
        /// Junto con CLV_SUC forma la clave de negocio única.
        /// </summary>
        public int ID_PERIODO { get; set; } = 0;

        [Column(TypeName = "date")]
        public DateTime? FECHA_INI_ES { get; set; }

        [Column(TypeName = "date")]
        public DateTime? FECHA_FIN_ES { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? OBSERVACIONES { get; set; }
    }
}
