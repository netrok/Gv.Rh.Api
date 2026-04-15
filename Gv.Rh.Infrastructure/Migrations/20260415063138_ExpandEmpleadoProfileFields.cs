using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandEmpleadoProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_empleado_documentos_empleados_EmpleadoId",
                table: "empleado_documentos");

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "empleados",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactoEmergenciaNombre",
                table: "empleados",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactoEmergenciaParentesco",
                table: "empleados",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactoEmergenciaTelefono",
                table: "empleados",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Curp",
                table: "empleados",
                type: "character varying(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionCalle",
                table: "empleados",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionCiudad",
                table: "empleados",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionCodigoPostal",
                table: "empleados",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionColonia",
                table: "empleados",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionEstado",
                table: "empleados",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionNumeroExterior",
                table: "empleados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionNumeroInterior",
                table: "empleados",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoCivil",
                table: "empleados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FotoMimeType",
                table: "empleados",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoNombreOriginal",
                table: "empleados",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoRutaRelativa",
                table: "empleados",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FotoTamanoBytes",
                table: "empleados",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FotoUpdatedAtUtc",
                table: "empleados",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nacionalidad",
                table: "empleados",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nss",
                table: "empleados",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rfc",
                table: "empleados",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sexo",
                table: "empleados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_Activo",
                table: "empleados",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_Curp",
                table: "empleados",
                column: "Curp");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_Email",
                table: "empleados",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_Nss",
                table: "empleados",
                column: "Nss");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_Rfc",
                table: "empleados",
                column: "Rfc");

            migrationBuilder.AddForeignKey(
                name: "FK_empleado_documentos_empleados_EmpleadoId",
                table: "empleado_documentos",
                column: "EmpleadoId",
                principalTable: "empleados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_empleado_documentos_empleados_EmpleadoId",
                table: "empleado_documentos");

            migrationBuilder.DropIndex(
                name: "IX_empleados_Activo",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_Curp",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_Email",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_Nss",
                table: "empleados");

            migrationBuilder.DropIndex(
                name: "IX_empleados_Rfc",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "ContactoEmergenciaNombre",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "ContactoEmergenciaParentesco",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "ContactoEmergenciaTelefono",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "Curp",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DireccionCalle",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DireccionCiudad",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DireccionCodigoPostal",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DireccionColonia",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DireccionEstado",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DireccionNumeroExterior",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "DireccionNumeroInterior",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "EstadoCivil",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FotoMimeType",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FotoNombreOriginal",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FotoRutaRelativa",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FotoTamanoBytes",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FotoUpdatedAtUtc",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "Nacionalidad",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "Nss",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "Rfc",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "Sexo",
                table: "empleados");

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "empleados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_empleado_documentos_empleados_EmpleadoId",
                table: "empleado_documentos",
                column: "EmpleadoId",
                principalTable: "empleados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
