using System;
using Mantenimientos.Models;
using Microsoft.EntityFrameworkCore;

namespace Mantenimientos.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // Tablas principales
        public DbSet<Seguimiento> Seguimientos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Seguimiento>(entity =>
            {
                entity.ToTable("Seguimientos");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.RUTA).IsRequired();
                entity.Property(e => e.SUCURSAL).IsRequired().HasMaxLength(200);

                entity.Property(e => e.FECHA_INI_ES).HasColumnType("date").HasDefaultValue(new DateTime(1900, 1, 1));
                entity.Property(e => e.FECHA_FIN_ES).HasColumnType("date").HasDefaultValue(new DateTime(1900, 1, 1));
                entity.Property(e => e.FECHA_INI_RE).HasColumnType("date").HasDefaultValue(new DateTime(1900, 1, 1));
                entity.Property(e => e.FECHA_FIN_RE).HasColumnType("date").HasDefaultValue(new DateTime(1900, 1, 1));

                entity.Property(e => e.DIAS_ATRASO).HasDefaultValue(0);
                entity.Property(e => e.OBSERVACIONES).HasColumnType("nvarchar(max)");

                // filtros del index
                entity.HasIndex(e => e.RUTA).HasDatabaseName("IX_Seguimiento_Ruta");
                entity.HasIndex(e => e.SUCURSAL).HasDatabaseName("IX_Seguimiento_Sucursal");
                entity.HasIndex(e => e.FECHA_INI_ES).HasDatabaseName("IX_Seguimiento_FechaIniEst");
            });
        }
    }
}