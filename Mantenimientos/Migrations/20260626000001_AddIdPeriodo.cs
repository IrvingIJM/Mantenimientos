using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mantenimientos.Migrations
{
    public partial class AddIdPeriodo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM   sys.columns
                    WHERE  object_id = OBJECT_ID(N'dbo.Seguimientos')
                      AND  name      = N'ID_PERIODO'
                )
                BEGIN
                    ALTER TABLE dbo.Seguimientos
                        ADD ID_PERIODO INT NOT NULL
                            CONSTRAINT DF_Seguimientos_IdPeriodo DEFAULT 0;
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM   sys.indexes
                    WHERE  object_id = OBJECT_ID(N'dbo.Seguimientos')
                      AND  name      = N'IX_Seguimiento_ClvSuc'
                )
                BEGIN
                    DROP INDEX IX_Seguimiento_ClvSuc ON dbo.Seguimientos;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM   sys.indexes
                    WHERE  object_id = OBJECT_ID(N'dbo.Seguimientos')
                      AND  name      = N'IX_Seguimiento_ClvSuc_Periodo'
                )
                BEGIN
                    CREATE UNIQUE INDEX IX_Seguimiento_ClvSuc_Periodo
                        ON dbo.Seguimientos (CLV_SUC, ID_PERIODO);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM   sys.indexes
                    WHERE  object_id = OBJECT_ID(N'dbo.Seguimientos')
                      AND  name      = N'IX_Seguimiento_ClvSuc_Periodo'
                )
                    DROP INDEX IX_Seguimiento_ClvSuc_Periodo ON dbo.Seguimientos;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM   sys.columns
                    WHERE  object_id = OBJECT_ID(N'dbo.Seguimientos')
                      AND  name      = N'ID_PERIODO'
                )
                BEGIN
                    ALTER TABLE dbo.Seguimientos
                        DROP CONSTRAINT DF_Seguimientos_IdPeriodo;
                    ALTER TABLE dbo.Seguimientos
                        DROP COLUMN ID_PERIODO;
                END
            ");
        }
    }
}