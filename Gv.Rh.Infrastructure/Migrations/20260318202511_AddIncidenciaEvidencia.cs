using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidenciaEvidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenciaContentType",
                table: "incidencias",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenciaNombreArchivo",
                table: "incidencias",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenciaNombreOriginal",
                table: "incidencias",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenciaRuta",
                table: "incidencias",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EvidenciaTamanoBytes",
                table: "incidencias",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TieneEvidencia",
                table: "incidencias",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenciaContentType",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "EvidenciaNombreArchivo",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "EvidenciaNombreOriginal",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "EvidenciaRuta",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "EvidenciaTamanoBytes",
                table: "incidencias");

            migrationBuilder.DropColumn(
                name: "TieneEvidencia",
                table: "incidencias");
        }
    }
}
