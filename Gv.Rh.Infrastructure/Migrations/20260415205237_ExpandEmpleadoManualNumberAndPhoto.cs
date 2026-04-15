using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandEmpleadoManualNumberAndPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "Recontratable",
                table: "empleados",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoPostalFiscal",
                table: "empleados",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "empleados",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "EntidadFiscal",
                table: "empleados",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoNombreGuardado",
                table: "empleados",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "empleados",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoPostalFiscal",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "EntidadFiscal",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "FotoNombreGuardado",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "empleados");

            migrationBuilder.AlterColumn<bool>(
                name: "Recontratable",
                table: "empleados",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}
