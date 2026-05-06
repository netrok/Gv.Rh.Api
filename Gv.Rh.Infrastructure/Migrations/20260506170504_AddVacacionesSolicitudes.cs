using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVacacionesSolicitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vacacion_solicitudes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    VacacionPeriodoId = table.Column<int>(type: "integer", nullable: true),
                    VacacionMovimientoId = table.Column<int>(type: "integer", nullable: true),
                    IncidenciaId = table.Column<int>(type: "integer", nullable: true),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    DiasSolicitados = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    Estatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ComentarioEmpleado = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ComentarioResolucion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MotivoRechazo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SolicitadaPorUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    ResueltaPorUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    AprobadorEmpleadoId = table.Column<int>(type: "integer", nullable: true),
                    FechaResolucionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacacion_solicitudes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitudes_empleados_AprobadorEmpleadoId",
                        column: x => x.AprobadorEmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitudes_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitudes_incidencias_IncidenciaId",
                        column: x => x.IncidenciaId,
                        principalTable: "incidencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitudes_users_ResueltaPorUsuarioId",
                        column: x => x.ResueltaPorUsuarioId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitudes_users_SolicitadaPorUsuarioId",
                        column: x => x.SolicitadaPorUsuarioId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitudes_vacacion_movimientos_VacacionMovimient~",
                        column: x => x.VacacionMovimientoId,
                        principalTable: "vacacion_movimientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitudes_vacacion_periodos_VacacionPeriodoId",
                        column: x => x.VacacionPeriodoId,
                        principalTable: "vacacion_periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_AprobadorEmpleadoId",
                table: "vacacion_solicitudes",
                column: "AprobadorEmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_CreatedAtUtc",
                table: "vacacion_solicitudes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_EmpleadoId",
                table: "vacacion_solicitudes",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_EmpleadoId_Estatus",
                table: "vacacion_solicitudes",
                columns: new[] { "EmpleadoId", "Estatus" });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_Estatus",
                table: "vacacion_solicitudes",
                column: "Estatus");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_FechaFin",
                table: "vacacion_solicitudes",
                column: "FechaFin");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_FechaInicio",
                table: "vacacion_solicitudes",
                column: "FechaInicio");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_IncidenciaId",
                table: "vacacion_solicitudes",
                column: "IncidenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_ResueltaPorUsuarioId",
                table: "vacacion_solicitudes",
                column: "ResueltaPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_SolicitadaPorUsuarioId",
                table: "vacacion_solicitudes",
                column: "SolicitadaPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_VacacionMovimientoId",
                table: "vacacion_solicitudes",
                column: "VacacionMovimientoId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitudes_VacacionPeriodoId",
                table: "vacacion_solicitudes",
                column: "VacacionPeriodoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vacacion_solicitudes");
        }
    }
}
