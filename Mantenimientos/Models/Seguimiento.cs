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
        public int RUTA { get; set; }

        [Required]
        [MaxLength(200)]
        public string SUCURSAL { get; set; } = string.Empty;

        [Column(TypeName = "date")]
        public DateTime FECHA_INI_ES { get; set; } = new DateTime(1900, 1, 1);
        [Column(TypeName = "date")]
        public DateTime FECHA_FIN_ES { get; set; } = new DateTime(1900, 1, 1);

        [Column(TypeName = "date")]
        public DateTime FECHA_INI_RE { get; set; } = new DateTime(1900, 1, 1);

        [Column(TypeName = "date")]
        public DateTime FECHA_FIN_RE { get; set; } = new DateTime(1900, 1, 1);

        public int DIAS_ATRASO { get; set; } = 0;

        [Column(TypeName = "nvarchar(max)")]
        public string? OBSERVACIONES { get; set; }

    }
}
