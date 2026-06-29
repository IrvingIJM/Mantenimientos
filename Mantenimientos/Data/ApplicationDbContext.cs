using Mantenimientos.Models;
using Microsoft.EntityFrameworkCore;

namespace Mantenimientos.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Seguimiento> Seguimientos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Seguimiento>(entity =>
            {
                entity.ToTable("Seguimientos");
                entity.HasKey(e => e.ID);

                entity.Property(e => e.CLV_SUC)
                      .IsRequired()
                      .HasMaxLength(50);

                // Periodo: obligatorio, default 0 (se sobreescribe al importar)
                entity.Property(e => e.ID_PERIODO)
                      .IsRequired()
                      .HasDefaultValue(0);

                entity.Property(e => e.FECHA_INI_ES)
                      .HasColumnType("date");

                entity.Property(e => e.FECHA_FIN_ES)
                      .HasColumnType("date");

                entity.Property(e => e.OBSERVACIONES)
                      .HasColumnType("nvarchar(max)");

                // ── Índices ───────────────────────────────────────────────────

                // Clave de negocio: una sucursal no puede tener dos registros
                // para el mismo periodo.
                entity.HasIndex(e => new { e.CLV_SUC, e.ID_PERIODO })
                      .HasDatabaseName("IX_Seguimiento_ClvSuc_Periodo")
                      .IsUnique();

                // Auxiliar para filtros por fecha estimada
                entity.HasIndex(e => e.FECHA_INI_ES)
                      .HasDatabaseName("IX_Seguimiento_FechaIniEst");
            });
        }
    }
}
