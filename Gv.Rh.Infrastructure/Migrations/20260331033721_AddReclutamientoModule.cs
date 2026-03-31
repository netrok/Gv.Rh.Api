using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReclutamientoModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "vacantes_folio_seq");

            migrationBuilder.CreateTable(
                name: "reclutamiento_candidatos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombres = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Ciudad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FuenteReclutamiento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResumenPerfil = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PretensionSalarial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CvNombreOriginal = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    CvNombreGuardado = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    CvRutaRelativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CvMimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CvTamanoBytes = table.Column<long>(type: "bigint", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reclutamiento_candidatos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reclutamiento_vacantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Folio = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DepartamentoId = table.Column<int>(type: "integer", nullable: false),
                    PuestoId = table.Column<int>(type: "integer", nullable: false),
                    SucursalId = table.Column<int>(type: "integer", nullable: false),
                    NumeroPosiciones = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Perfil = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SalarioMinimo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalarioMaximo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FechaApertura = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaCierre = table.Column<DateOnly>(type: "date", nullable: true),
                    Estatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reclutamiento_vacantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reclutamiento_vacantes_departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reclutamiento_vacantes_puestos_PuestoId",
                        column: x => x.PuestoId,
                        principalTable: "puestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reclutamiento_vacantes_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reclutamiento_postulaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VacanteId = table.Column<int>(type: "integer", nullable: false),
                    CandidatoId = table.Column<int>(type: "integer", nullable: false),
                    EtapaActual = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaPostulacionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaUltimoMovimientoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservacionesInternas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MotivoDescarte = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EsContratado = table.Column<bool>(type: "boolean", nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: true),
                    FechaContratacionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reclutamiento_postulaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reclutamiento_postulaciones_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reclutamiento_postulaciones_reclutamiento_candidatos_Candid~",
                        column: x => x.CandidatoId,
                        principalTable: "reclutamiento_candidatos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reclutamiento_postulaciones_reclutamiento_vacantes_VacanteId",
                        column: x => x.VacanteId,
                        principalTable: "reclutamiento_vacantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reclutamiento_postulaciones_seguimiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostulacionId = table.Column<int>(type: "integer", nullable: false),
                    EtapaAnterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EtapaNueva = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Comentario = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreadoPorUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reclutamiento_postulaciones_seguimiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reclutamiento_postulaciones_seguimiento_reclutamiento_postu~",
                        column: x => x.PostulacionId,
                        principalTable: "reclutamiento_postulaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reclutamiento_postulaciones_seguimiento_users_CreadoPorUser~",
                        column: x => x.CreadoPorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_candidatos_Activo",
                table: "reclutamiento_candidatos",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_candidatos_Email",
                table: "reclutamiento_candidatos",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_candidatos_FuenteReclutamiento",
                table: "reclutamiento_candidatos",
                column: "FuenteReclutamiento");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_candidatos_Telefono",
                table: "reclutamiento_candidatos",
                column: "Telefono");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_CandidatoId",
                table: "reclutamiento_postulaciones",
                column: "CandidatoId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_EmpleadoId",
                table: "reclutamiento_postulaciones",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_EsContratado",
                table: "reclutamiento_postulaciones",
                column: "EsContratado");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_EtapaActual",
                table: "reclutamiento_postulaciones",
                column: "EtapaActual");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_VacanteId",
                table: "reclutamiento_postulaciones",
                column: "VacanteId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_VacanteId_CandidatoId",
                table: "reclutamiento_postulaciones",
                columns: new[] { "VacanteId", "CandidatoId" });

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_seguimiento_CreadoPorUserId",
                table: "reclutamiento_postulaciones_seguimiento",
                column: "CreadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_seguimiento_CreatedAtUtc",
                table: "reclutamiento_postulaciones_seguimiento",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_postulaciones_seguimiento_PostulacionId",
                table: "reclutamiento_postulaciones_seguimiento",
                column: "PostulacionId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_vacantes_DepartamentoId",
                table: "reclutamiento_vacantes",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_vacantes_Estatus",
                table: "reclutamiento_vacantes",
                column: "Estatus");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_vacantes_Folio",
                table: "reclutamiento_vacantes",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_vacantes_PuestoId",
                table: "reclutamiento_vacantes",
                column: "PuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_vacantes_SucursalId",
                table: "reclutamiento_vacantes",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_reclutamiento_vacantes_Titulo",
                table: "reclutamiento_vacantes",
                column: "Titulo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reclutamiento_postulaciones_seguimiento");

            migrationBuilder.DropTable(
                name: "reclutamiento_postulaciones");

            migrationBuilder.DropTable(
                name: "reclutamiento_candidatos");

            migrationBuilder.DropTable(
                name: "reclutamiento_vacantes");

            migrationBuilder.DropSequence(
                name: "vacantes_folio_seq");
        }
    }
}
