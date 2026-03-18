using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidenciasModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incidencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    SucursalId = table.Column<int>(type: "integer", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Estatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incidencias_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidencias_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_EmpleadoId",
                table: "incidencias",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_Estatus",
                table: "incidencias",
                column: "Estatus");

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_FechaFin",
                table: "incidencias",
                column: "FechaFin");

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_FechaInicio",
                table: "incidencias",
                column: "FechaInicio");

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_SucursalId",
                table: "incidencias",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_Tipo",
                table: "incidencias",
                column: "Tipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incidencias");
        }
    }
}
