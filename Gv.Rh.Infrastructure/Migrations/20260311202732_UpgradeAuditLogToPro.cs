using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    public partial class UpgradeAuditLogToPro : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_audit_log",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_OccurredAtUtc",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_Entity",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_EntityId",
                table: "audit_log");

            migrationBuilder.RenameTable(
                name: "audit_log",
                newName: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "audit_logs",
                newName: "UserEmail");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "audit_logs",
                newName: "UserRole");

            migrationBuilder.RenameColumn(
                name: "Entity",
                table: "audit_logs",
                newName: "EntityName");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "audit_logs",
                newName: "RecordId");

            migrationBuilder.RenameColumn(
                name: "Ip",
                table: "audit_logs",
                newName: "IpAddress");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "RecordId",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "UserEmail",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserRole",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "audit_logs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldValuesJson",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewValuesJson",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedColumnsJson",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "audit_logs"
                SET
                    "OldValuesJson" = CASE
                        WHEN "ChangesJson" IS NOT NULL AND ("ChangesJson" ? 'before')
                            THEN "ChangesJson" -> 'before'
                        ELSE NULL
                    END,
                    "NewValuesJson" = CASE
                        WHEN "ChangesJson" IS NOT NULL AND ("ChangesJson" ? 'after')
                            THEN "ChangesJson" -> 'after'
                        ELSE NULL
                    END,
                    "ChangedColumnsJson" = CASE
                        WHEN "ChangesJson" IS NOT NULL AND ("ChangesJson" ? 'after')
                            THEN (
                                SELECT COALESCE(jsonb_agg(k), '[]'::jsonb)
                                FROM jsonb_object_keys("ChangesJson" -> 'after') AS k
                            )
                        WHEN "ChangesJson" IS NOT NULL AND ("ChangesJson" ? 'before')
                            THEN (
                                SELECT COALESCE(jsonb_agg(k), '[]'::jsonb)
                                FROM jsonb_object_keys("ChangesJson" -> 'before') AS k
                            )
                        ELSE NULL
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "ChangesJson",
                table: "audit_logs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_audit_logs",
                table: "audit_logs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OccurredAtUtc",
                table: "audit_logs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Action",
                table: "audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityName",
                table: "audit_logs",
                column: "EntityName");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityName_RecordId",
                table: "audit_logs",
                columns: new[] { "EntityName", "RecordId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_audit_logs",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_OccurredAtUtc",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_Action",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_EntityName",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_EntityName_RecordId",
                table: "audit_logs");

            migrationBuilder.AddColumn<string>(
                name: "ChangesJson",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "audit_logs"
                SET "ChangesJson" = CASE
                    WHEN "OldValuesJson" IS NOT NULL AND "NewValuesJson" IS NOT NULL
                        THEN jsonb_build_object('before', "OldValuesJson", 'after', "NewValuesJson")
                    WHEN "NewValuesJson" IS NOT NULL
                        THEN jsonb_build_object('after', "NewValuesJson")
                    WHEN "OldValuesJson" IS NOT NULL
                        THEN jsonb_build_object('before', "OldValuesJson")
                    ELSE NULL
                END;
                """);

            migrationBuilder.DropColumn(
                name: "OldValuesJson",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "NewValuesJson",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ChangedColumnsJson",
                table: "audit_logs");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "audit_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "audit_logs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "RecordId",
                table: "audit_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "UserEmail",
                table: "audit_logs",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserRole",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "audit_logs",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "audit_logs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "UserEmail",
                table: "audit_logs",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "UserRole",
                table: "audit_logs",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "EntityName",
                table: "audit_logs",
                newName: "Entity");

            migrationBuilder.RenameColumn(
                name: "RecordId",
                table: "audit_logs",
                newName: "EntityId");

            migrationBuilder.RenameColumn(
                name: "IpAddress",
                table: "audit_logs",
                newName: "Ip");

            migrationBuilder.RenameTable(
                name: "audit_logs",
                newName: "audit_log");

            migrationBuilder.AddPrimaryKey(
                name: "PK_audit_log",
                table: "audit_log",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_OccurredAtUtc",
                table: "audit_log",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_Entity",
                table: "audit_log",
                column: "Entity");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_EntityId",
                table: "audit_log",
                column: "EntityId");
        }
    }
}