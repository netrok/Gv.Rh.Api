using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Users_Link_Empleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpleadoId",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_EmpleadoId",
                table: "users",
                column: "EmpleadoId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_users_empleados_EmpleadoId",
                table: "users",
                column: "EmpleadoId",
                principalTable: "empleados",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_empleados_EmpleadoId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_EmpleadoId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmpleadoId",
                table: "users");
        }
    }
}
