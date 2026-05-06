using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificacion_outbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DestinatariosJson = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    MaxIntentos = table.Column<int>(type: "integer", nullable: false),
                    UltimoError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NextAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CorrelationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificacion_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_outbox_CorrelationType_CorrelationId",
                table: "notificacion_outbox",
                columns: new[] { "CorrelationType", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_outbox_CreatedAtUtc",
                table: "notificacion_outbox",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_outbox_Estado",
                table: "notificacion_outbox",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_notificacion_outbox_NextAttemptUtc",
                table: "notificacion_outbox",
                column: "NextAttemptUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacion_outbox");
        }
    }
}
