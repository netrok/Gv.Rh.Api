using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpleadoMovimientosLaborales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstatusLaboralActual",
                table: "empleados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaBajaActual",
                table: "empleados",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaReingresoActual",
                table: "empleados",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Recontratable",
                table: "empleados",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoBajaActual",
                table: "empleados",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "empleado_movimientos_laborales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    TipoMovimiento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaMovimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoBaja = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Recontratable = table.Column<bool>(type: "boolean", nullable: true),
                    UsuarioResponsableId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleado_movimientos_laborales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_empleado_movimientos_laborales_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_empleados_EstatusLaboralActual",
                table: "empleados",
                column: "EstatusLaboralActual");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_movimientos_laborales_EmpleadoId",
                table: "empleado_movimientos_laborales",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_movimientos_laborales_FechaMovimiento",
                table: "empleado_movimientos_laborales",
                column: "FechaMovimiento");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_movimientos_laborales_TipoMovimiento",
                table: "empleado_movimientos_laborales",
                column: "TipoMovimiento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "empleado_movimientos_laborales");

            migrationBuilder.DropIndex(
                name: "IX_empleados_EstatusLaboralActual",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "EstatusLaboralActual",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FechaBajaActual",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FechaReingresoActual",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "Recontratable",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "TipoBajaActual",
                table: "empleados");
        }
    }
}
