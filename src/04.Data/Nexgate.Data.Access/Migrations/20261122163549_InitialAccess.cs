using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexgate.Common.BuildingBlocks.Persistence;

#nullable disable

namespace Nexgate.Data.Access.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.CreateTable(
                name: "credential",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credential", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    windows = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grant",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effect = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    origin_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    remaining_uses = table.Column<int>(type: "integer", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant", x => x.id);
                    table.ForeignKey(
                        name: "fk_grant_schedule",
                        column: x => x.schedule_id,
                        principalSchema: "access",
                        principalTable: "schedule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credential_organization_id",
                schema: "access",
                table: "credential",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_credential_subject_id",
                schema: "access",
                table: "credential",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ux_credential_site_type_hash",
                schema: "access",
                table: "credential",
                columns: new[] { "site_id", "type", "value_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grant_schedule_id",
                schema: "access",
                table: "grant",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_grant_organization_id",
                schema: "access",
                table: "grant",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_grant_site_target",
                schema: "access",
                table: "grant",
                columns: new[] { "site_id", "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_grant_subject_id",
                schema: "access",
                table: "grant",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_organization_id",
                schema: "access",
                table: "schedule",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_site_id",
                schema: "access",
                table: "schedule",
                column: "site_id");

            // Nombre de horario único por sitio sin distinguir mayúsculas (design de add-access-evaluator, decisión 3).
            migrationBuilder.Sql(
                $"CREATE UNIQUE INDEX {Nexgate.Data.Access.AccessDbContext.ScheduleNameIndex} ON access.schedule (site_id, lower(name));");

            migrationBuilder.EnableTenantIsolation("access", "schedule");
            migrationBuilder.EnableTenantIsolation("access", "credential");
            migrationBuilder.EnableTenantIsolation("access", "grant");
            migrationBuilder.GrantAppAccess("access");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credential",
                schema: "access");

            migrationBuilder.DropTable(
                name: "grant",
                schema: "access");

            migrationBuilder.DropTable(
                name: "schedule",
                schema: "access");
        }
    }
}
