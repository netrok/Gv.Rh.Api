using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpleadoDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "empleado_documentos",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Tipo",
                table: "empleado_documentos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ActualizadoPorUsuarioId",
                table: "empleado_documentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreadoPorUsuarioId",
                table: "empleado_documentos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualizadoPorUsuarioId",
                table: "empleado_documentos");

            migrationBuilder.DropColumn(
                name: "CreadoPorUsuarioId",
                table: "empleado_documentos");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "empleado_documentos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Tipo",
                table: "empleado_documentos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
