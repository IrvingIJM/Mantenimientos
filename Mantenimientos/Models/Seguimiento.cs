using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mantenimientos.Models
{
    [Table("Seguimientos")]
    public class Seguimiento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        [MaxLength(50)]
        public string CLV_SUC { get; set; } = string.Empty;

        public int ID_PERIODO { get; set; } = 0;

        [Column(TypeName = "date")]
        public DateTime? FECHA_INI_ES { get; set; }

        [Column(TypeName = "date")]
        public DateTime? FECHA_FIN_ES { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? OBSERVACIONES { get; set; }
    }
}