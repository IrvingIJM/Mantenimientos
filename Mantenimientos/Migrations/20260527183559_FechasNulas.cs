using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mantenimientos.Migrations
{
    /// <inheritdoc />
    public partial class FechasNulas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_INI_RE",
                table: "Seguimientos",
                type: "date",
                nullable: true,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_INI_ES",
                table: "Seguimientos",
                type: "date",
                nullable: true,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_FIN_RE",
                table: "Seguimientos",
                type: "date",
                nullable: true,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_FIN_ES",
                table: "Seguimientos",
                type: "date",
                nullable: true,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "DIAS_ATRASO",
                table: "Seguimientos",
                type: "int",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_INI_RE",
                table: "Seguimientos",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true,
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_INI_ES",
                table: "Seguimientos",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true,
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_FIN_RE",
                table: "Seguimientos",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true,
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<DateTime>(
                name: "FECHA_FIN_ES",
                table: "Seguimientos",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true,
                oldDefaultValue: new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "DIAS_ATRASO",
                table: "Seguimientos",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldDefaultValue: 0);
        }
    }
}
