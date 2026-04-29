using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVacacionesKardexBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vacacion_politicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    PrimaVacacionalPorcentaje = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacacion_politicas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vacacion_periodos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    VacacionPoliticaId = table.Column<int>(type: "integer", nullable: true),
                    AnioServicio = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaLimiteDisfrute = table.Column<DateOnly>(type: "date", nullable: false),
                    DiasDerecho = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    DiasTomados = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    DiasPagados = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    DiasAjustados = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    DiasVencidos = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    Saldo = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    PrimaPagada = table.Column<bool>(type: "boolean", nullable: false),
                    FechaPagoPrima = table.Column<DateOnly>(type: "date", nullable: true),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Estatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacacion_periodos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vacacion_periodos_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacacion_periodos_vacacion_politicas_VacacionPoliticaId",
                        column: x => x.VacacionPoliticaId,
                        principalTable: "vacacion_politicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vacacion_politica_detalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VacacionPoliticaId = table.Column<int>(type: "integer", nullable: false),
                    AnioServicioDesde = table.Column<int>(type: "integer", nullable: false),
                    AnioServicioHasta = table.Column<int>(type: "integer", nullable: true),
                    DiasVacaciones = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacacion_politica_detalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vacacion_politica_detalles_vacacion_politicas_VacacionPolit~",
                        column: x => x.VacacionPoliticaId,
                        principalTable: "vacacion_politicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacacion_movimientos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    VacacionPeriodoId = table.Column<int>(type: "integer", nullable: false),
                    TipoMovimiento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FechaMovimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaInicioDisfrute = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFinDisfrute = table.Column<DateOnly>(type: "date", nullable: true),
                    Dias = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    SaldoAntes = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    SaldoDespues = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    Referencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UsuarioResponsableId = table.Column<int>(type: "integer", nullable: true),
                    Origen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImportacionArchivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImportacionHoja = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ImportacionFila = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacacion_movimientos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vacacion_movimientos_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vacacion_movimientos_users_UsuarioResponsableId",
                        column: x => x.UsuarioResponsableId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vacacion_movimientos_vacacion_periodos_VacacionPeriodoId",
                        column: x => x.VacacionPeriodoId,
                        principalTable: "vacacion_periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_CreatedAtUtc",
                table: "vacacion_movimientos",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_EmpleadoId",
                table: "vacacion_movimientos",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_EmpleadoId_FechaMovimiento",
                table: "vacacion_movimientos",
                columns: new[] { "EmpleadoId", "FechaMovimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_FechaMovimiento",
                table: "vacacion_movimientos",
                column: "FechaMovimiento");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_TipoMovimiento",
                table: "vacacion_movimientos",
                column: "TipoMovimiento");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_UsuarioResponsableId",
                table: "vacacion_movimientos",
                column: "UsuarioResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_movimientos_VacacionPeriodoId",
                table: "vacacion_movimientos",
                column: "VacacionPeriodoId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_AnioServicio",
                table: "vacacion_periodos",
                column: "AnioServicio");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_EmpleadoId",
                table: "vacacion_periodos",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_EmpleadoId_AnioServicio",
                table: "vacacion_periodos",
                columns: new[] { "EmpleadoId", "AnioServicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_EmpleadoId_FechaInicio_FechaFin",
                table: "vacacion_periodos",
                columns: new[] { "EmpleadoId", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_Estatus",
                table: "vacacion_periodos",
                column: "Estatus");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_periodos_VacacionPoliticaId",
                table: "vacacion_periodos",
                column: "VacacionPoliticaId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_politica_detalles_VacacionPoliticaId",
                table: "vacacion_politica_detalles",
                column: "VacacionPoliticaId");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_politica_detalles_VacacionPoliticaId_AnioServicioD~",
                table: "vacacion_politica_detalles",
                columns: new[] { "VacacionPoliticaId", "AnioServicioDesde", "AnioServicioHasta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_politicas_Activo",
                table: "vacacion_politicas",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_politicas_Nombre",
                table: "vacacion_politicas",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_politicas_VigenteDesde_VigenteHasta",
                table: "vacacion_politicas",
                columns: new[] { "VigenteDesde", "VigenteHasta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vacacion_movimientos");

            migrationBuilder.DropTable(
                name: "vacacion_politica_detalles");

            migrationBuilder.DropTable(
                name: "vacacion_periodos");

            migrationBuilder.DropTable(
                name: "vacacion_politicas");
        }
    }
}
