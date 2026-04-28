using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpleadoAprobadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AprobadorPrimarioEmpleadoId",
                table: "empleados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AprobadorSecundarioEmpleadoId",
                table: "empleados",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_empleados_AprobadorPrimarioEmpleadoId",
                table: "empleados",
                column: "AprobadorPrimarioEmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_AprobadorSecundarioEmpleadoId",
                table: "empleados",
                column: "AprobadorSecundarioEmpleadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_empleados_empleados_AprobadorPrimarioEmpleadoId",
                table: "empleados",
                column: "AprobadorPrimarioEmpleadoId",
                principalTable: "empleados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_empleados_empleados_AprobadorSecundarioEmpleadoId",
                table: "empleados",
                column: "AprobadorSecundarioEmpleadoId",
                principalTable: "empleados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_empleados_empleados_AprobadorPrimarioEmpleadoId",
                table: "empleados");

            migrationBuilder.DropForeignKey(
                name: "FK_empleados_empleados_AprobadorSecundarioEmpleadoId",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_AprobadorPrimarioEmpleadoId",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_AprobadorSecundarioEmpleadoId",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "AprobadorPrimarioEmpleadoId",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "AprobadorSecundarioEmpleadoId",
                table: "empleados");
        }
    }
}
