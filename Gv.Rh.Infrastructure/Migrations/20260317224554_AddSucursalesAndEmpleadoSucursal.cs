using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSucursalesAndEmpleadoSucursal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SucursalId",
                table: "empleados",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sucursales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Direccion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sucursales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_empleados_SucursalId",
                table: "empleados",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_Clave",
                table: "sucursales",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_Nombre",
                table: "sucursales",
                column: "Nombre");

            migrationBuilder.AddForeignKey(
                name: "FK_empleados_sucursales_SucursalId",
                table: "empleados",
                column: "SucursalId",
                principalTable: "sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_empleados_sucursales_SucursalId",
                table: "empleados");

            migrationBuilder.DropTable(
                name: "sucursales");

            migrationBuilder.DropIndex(
                name: "IX_empleados_SucursalId",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "empleados");
        }
    }
}
