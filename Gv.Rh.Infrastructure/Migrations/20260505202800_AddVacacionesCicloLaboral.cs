using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVacacionesCicloLaboral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vacacion_periodos_EmpleadoId_AnioServicio",
                table: "vacacion_periodos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_periodos_EmpleadoId_FechaInicio_FechaFin",
                table: "vacacion_periodos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_movimientos_CreatedAtUtc",
                table: "vacacion_movimientos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_movimientos_EmpleadoId_FechaMovimiento",
                table: "vacacion_movimientos");

            migrationBuilder.AddColumn<int>(
                name: "CicloLaboral",
                table: "vacacion_periodos",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "Referencia",
                table: "vacacion_movimientos",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Origen",
                table: "vacacion_movimientos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImportacionHoja",
                table: "vacacion_movimientos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImportacionArchivo",
                table: "vacacion_movimientos",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comentario",
                table: "vacacion_movimientos",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CicloLaboral",
                table: "vacacion_movimientos",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_CicloLaboral",
                table: "vacacion_periodos",
                column: "CicloLaboral");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_EmpleadoId_CicloLaboral_AnioServicio",
                table: "vacacion_periodos",
                columns: new[] { "EmpleadoId", "CicloLaboral", "AnioServicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_EmpleadoId_CicloLaboral_FechaInicio_Fecha~",
                table: "vacacion_periodos",
                columns: new[] { "EmpleadoId", "CicloLaboral", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_CicloLaboral",
                table: "vacacion_movimientos",
                column: "CicloLaboral");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_EmpleadoId_CicloLaboral_FechaMovimiento",
                table: "vacacion_movimientos",
                columns: new[] { "EmpleadoId", "CicloLaboral", "FechaMovimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_Origen",
                table: "vacacion_movimientos",
                column: "Origen");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_VacacionPeriodoId_FechaMovimiento",
                table: "vacacion_movimientos",
                columns: new[] { "VacacionPeriodoId", "FechaMovimiento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vacacion_periodos_CicloLaboral",
                table: "vacacion_periodos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_periodos_EmpleadoId_CicloLaboral_AnioServicio",
                table: "vacacion_periodos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_periodos_EmpleadoId_CicloLaboral_FechaInicio_Fecha~",
                table: "vacacion_periodos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_movimientos_CicloLaboral",
                table: "vacacion_movimientos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_movimientos_EmpleadoId_CicloLaboral_FechaMovimiento",
                table: "vacacion_movimientos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_movimientos_Origen",
                table: "vacacion_movimientos");

            migrationBuilder.DropIndex(
                name: "IX_vacacion_movimientos_VacacionPeriodoId_FechaMovimiento",
                table: "vacacion_movimientos");

            migrationBuilder.DropColumn(
                name: "CicloLaboral",
                table: "vacacion_periodos");

            migrationBuilder.DropColumn(
                name: "CicloLaboral",
                table: "vacacion_movimientos");

            migrationBuilder.AlterColumn<string>(
                name: "Referencia",
                table: "vacacion_movimientos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(180)",
                oldMaxLength: 180,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Origen",
                table: "vacacion_movimientos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImportacionHoja",
                table: "vacacion_movimientos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImportacionArchivo",
                table: "vacacion_movimientos",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(260)",
                oldMaxLength: 260,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comentario",
                table: "vacacion_movimientos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_EmpleadoId_AnioServicio",
                table: "vacacion_periodos",
                columns: new[] { "EmpleadoId", "AnioServicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_EmpleadoId_FechaInicio_FechaFin",
                table: "vacacion_periodos",
                columns: new[] { "EmpleadoId", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_CreatedAtUtc",
                table: "vacacion_movimientos",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_EmpleadoId_FechaMovimiento",
                table: "vacacion_movimientos",
                columns: new[] { "EmpleadoId", "FechaMovimiento" });
        }
    }
}
