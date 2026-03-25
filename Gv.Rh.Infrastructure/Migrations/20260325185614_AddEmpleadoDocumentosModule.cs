using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpleadoDocumentosModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "empleado_documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    NombreArchivoOriginal = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    NombreArchivoGuardado = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    RutaRelativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleado_documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_empleado_documentos_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_empleado_documentos_Activo",
                table: "empleado_documentos",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_documentos_EmpleadoId",
                table: "empleado_documentos",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_documentos_EmpleadoId_Activo",
                table: "empleado_documentos",
                columns: new[] { "EmpleadoId", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_empleado_documentos_FechaVencimiento",
                table: "empleado_documentos",
                column: "FechaVencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_documentos_Tipo",
                table: "empleado_documentos",
                column: "Tipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "empleado_documentos");
        }
    }
}
