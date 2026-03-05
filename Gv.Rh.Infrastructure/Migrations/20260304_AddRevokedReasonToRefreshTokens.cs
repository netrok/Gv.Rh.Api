using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Persistence.Migrations;

public partial class AddRevokedReasonToRefreshTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "revoked_reason",
            table: "auth_refresh_tokens",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_auth_refresh_tokens_expires_at_utc",
            table: "auth_refresh_tokens",
            column: "expires_at_utc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_auth_refresh_tokens_expires_at_utc",
            table: "auth_refresh_tokens");

        migrationBuilder.DropColumn(
            name: "revoked_reason",
            table: "auth_refresh_tokens");
    }
}