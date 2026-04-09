using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "vacantes_folio_seq");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OldValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    NewValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ChangedColumnsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "departamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departamentos", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "puestos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DepartamentoId = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puestos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_puestos_departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "empleados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumEmpleado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombres = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FechaIngreso = table.Column<DateOnly>(type: "date", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    EstatusLaboralActual = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaBajaActual = table.Column<DateOnly>(type: "date", nullable: true),
                    TipoBajaActual = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FechaReingresoActual = table.Column<DateOnly>(type: "date", nullable: true),
                    Recontratable = table.Column<bool>(type: "boolean", nullable: true),
                    DepartamentoId = table.Column<int>(type: "integer", nullable: true),
                    PuestoId = table.Column<int>(type: "integer", nullable: true),
                    SucursalId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_empleados_departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_empleados_puestos_PuestoId",
                        column: x => x.PuestoId,
                        principalTable: "puestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_empleados_sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "empleado_documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NombreArchivoOriginal = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    NombreArchivoGuardado = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    RutaRelativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoPorUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    ActualizadoPorUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "empleado_movimientos_laborales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    TipoMovimiento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaMovimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoBaja = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Recontratable = table.Column<bool>(type: "boolean", nullable: true),
                    UsuarioResponsableId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleado_movimientos_laborales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_empleado_movimientos_laborales_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    TieneEvidencia = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenciaNombreOriginal = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    EvidenciaNombreArchivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    EvidenciaContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EvidenciaTamanoBytes = table.Column<long>(type: "bigint", nullable: true),
                    EvidenciaRuta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "CONSULTA"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "auth_refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auth_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_audit_logs_Action",
                table: "audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityName",
                table: "audit_logs",
                column: "EntityName");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityName_RecordId",
                table: "audit_logs",
                columns: new[] { "EntityName", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OccurredAtUtc",
                table: "audit_logs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_auth_refresh_tokens_ExpiresAtUtc",
                table: "auth_refresh_tokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_auth_refresh_tokens_TokenHash",
                table: "auth_refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auth_refresh_tokens_UserId",
                table: "auth_refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_departamentos_Clave",
                table: "departamentos",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_departamentos_Nombre",
                table: "departamentos",
                column: "Nombre");

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

            migrationBuilder.CreateIndex(
                name: "IX_empleado_movimientos_laborales_EmpleadoId",
                table: "empleado_movimientos_laborales",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_movimientos_laborales_FechaMovimiento",
                table: "empleado_movimientos_laborales",
                column: "FechaMovimiento");

            migrationBuilder.CreateIndex(
                name: "IX_empleado_movimientos_laborales_TipoMovimiento",
                table: "empleado_movimientos_laborales",
                column: "TipoMovimiento");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_DepartamentoId",
                table: "empleados",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_EstatusLaboralActual",
                table: "empleados",
                column: "EstatusLaboralActual");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_NumEmpleado",
                table: "empleados",
                column: "NumEmpleado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_empleados_PuestoId",
                table: "empleados",
                column: "PuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_SucursalId",
                table: "empleados",
                column: "SucursalId");

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

            migrationBuilder.CreateIndex(
                name: "IX_puestos_Clave",
                table: "puestos",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_puestos_DepartamentoId",
                table: "puestos",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_puestos_DepartamentoId_Nombre",
                table: "puestos",
                columns: new[] { "DepartamentoId", "Nombre" });

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

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_Clave",
                table: "sucursales",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_Nombre",
                table: "sucursales",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_EmpleadoId",
                table: "users",
                column: "EmpleadoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "auth_refresh_tokens");

            migrationBuilder.DropTable(
                name: "empleado_documentos");

            migrationBuilder.DropTable(
                name: "empleado_movimientos_laborales");

            migrationBuilder.DropTable(
                name: "incidencias");

            migrationBuilder.DropTable(
                name: "reclutamiento_postulaciones_seguimiento");

            migrationBuilder.DropTable(
                name: "reclutamiento_postulaciones");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "reclutamiento_candidatos");

            migrationBuilder.DropTable(
                name: "reclutamiento_vacantes");

            migrationBuilder.DropTable(
                name: "empleados");

            migrationBuilder.DropTable(
                name: "puestos");

            migrationBuilder.DropTable(
                name: "sucursales");

            migrationBuilder.DropTable(
                name: "departamentos");

            migrationBuilder.DropSequence(
                name: "vacantes_folio_seq");
        }
    }
}
