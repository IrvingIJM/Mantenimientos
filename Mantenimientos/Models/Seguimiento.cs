using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mantenimientos.Models
{
    // Tabla de Segimientos, base de datos: mttos
    [Table("Seguimientos")]
    public class Seguimiento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        [MaxLength(50)]
        public string CLV_SUC { get; set; } = string.Empty;

        [Column(TypeName = "date")]
        public DateTime? FECHA_INI_ES { get; set; }

        [Column(TypeName = "date")]
        public DateTime? FECHA_FIN_ES { get; set; }

        public int? DIAS_ATRASO { get; set; } = 0;

        [Column(TypeName = "nvarchar(max)")]
        public string? OBSERVACIONES { get; set; }
    }
}
