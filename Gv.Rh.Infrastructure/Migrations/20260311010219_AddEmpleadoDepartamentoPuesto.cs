using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpleadoDepartamentoPuesto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartamentoId",
                table: "empleados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PuestoId",
                table: "empleados",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_empleados_DepartamentoId",
                table: "empleados",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_PuestoId",
                table: "empleados",
                column: "PuestoId");

            migrationBuilder.AddForeignKey(
                name: "FK_empleados_departamentos_DepartamentoId",
                table: "empleados",
                column: "DepartamentoId",
                principalTable: "departamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_empleados_puestos_PuestoId",
                table: "empleados",
                column: "PuestoId",
                principalTable: "puestos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_empleados_departamentos_DepartamentoId",
                table: "empleados");

            migrationBuilder.DropForeignKey(
                name: "FK_empleados_puestos_PuestoId",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_DepartamentoId",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_PuestoId",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DepartamentoId",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "PuestoId",
                table: "empleados");
        }
    }
}
