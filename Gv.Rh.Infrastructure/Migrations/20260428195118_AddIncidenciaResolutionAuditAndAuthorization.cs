using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidenciaResolutionAuditAndAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComentarioResolucion",
                table: "incidencias",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaResolucionUtc",
                table: "incidencias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResueltaPorEmpleadoId",
                table: "incidencias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResueltaPorUsuarioId",
                table: "incidencias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_FechaResolucionUtc",
                table: "incidencias",
                column: "FechaResolucionUtc");

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_ResueltaPorEmpleadoId",
                table: "incidencias",
                column: "ResueltaPorEmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_ResueltaPorUsuarioId",
                table: "incidencias",
                column: "ResueltaPorUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_incidencias_empleados_ResueltaPorEmpleadoId",
                table: "incidencias",
                column: "ResueltaPorEmpleadoId",
                principalTable: "empleados",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_incidencias_users_ResueltaPorUsuarioId",
                table: "incidencias",
                column: "ResueltaPorUsuarioId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_incidencias_empleados_ResueltaPorEmpleadoId",
                table: "incidencias");

            migrationBuilder.DropForeignKey(
                name: "FK_incidencias_users_ResueltaPorUsuarioId",
                table: "incidencias");

            migrationBuilder.DropIndex(
                name: "IX_incidencias_FechaResolucionUtc",
                table: "incidencias");

            migrationBuilder.DropIndex(
                name: "IX_incidencias_ResueltaPorEmpleadoId",
                table: "incidencias");

            migrationBuilder.DropIndex(
                name: "IX_incidencias_ResueltaPorUsuarioId",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "ComentarioResolucion",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "FechaResolucionUtc",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "ResueltaPorEmpleadoId",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "ResueltaPorUsuarioId",
                table: "incidencias");
        }
    }
}
