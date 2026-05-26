using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mantenimientos.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Seguimientos",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RUTA = table.Column<int>(type: "int", nullable: false),
                    SUCURSAL = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FECHA_INI_ES = table.Column<DateTime>(type: "date", nullable: false, defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)),
                    FECHA_FIN_ES = table.Column<DateTime>(type: "date", nullable: false, defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)),
                    FECHA_INI_RE = table.Column<DateTime>(type: "date", nullable: false, defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)),
                    FECHA_FIN_RE = table.Column<DateTime>(type: "date", nullable: false, defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)),
                    DIAS_ATRASO = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OBSERVACIONES = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seguimientos", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Seguimiento_FechaIniEst",
                table: "Seguimientos",
                column: "FECHA_INI_ES");

            migrationBuilder.CreateIndex(
                name: "IX_Seguimiento_Ruta",
                table: "Seguimientos",
                column: "RUTA");

            migrationBuilder.CreateIndex(
                name: "IX_Seguimiento_Sucursal",
                table: "Seguimientos",
                column: "SUCURSAL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Seguimientos");
        }
    }
}
