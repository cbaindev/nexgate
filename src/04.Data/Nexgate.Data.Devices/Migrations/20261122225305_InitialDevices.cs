using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexgate.Common.BuildingBlocks.Persistence;

#nullable disable

namespace Nexgate.Data.Devices.Migrations
{
    /// <inheritdoc />
    public partial class InitialDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "devices");

            migrationBuilder.CreateTable(
                name: "device",
                schema: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    claim_code_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    claim_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    certificate_thumbprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    certificate_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    firmware_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reported_policy_version = table.Column<int>(type: "integer", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_access_point",
                schema: "devices",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_access_point", x => new { x.device_id, x.access_point_id });
                    table.ForeignKey(
                        name: "fk_device_access_point_device",
                        column: x => x.device_id,
                        principalSchema: "devices",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_policy",
                schema: "devices",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_policy", x => x.device_id);
                    table.ForeignKey(
                        name: "fk_device_policy_device",
                        column: x => x.device_id,
                        principalSchema: "devices",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_organization_id",
                schema: "devices",
                table: "device",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_site_id",
                schema: "devices",
                table: "device",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ux_device_certificate_thumbprint",
                schema: "devices",
                table: "device",
                column: "certificate_thumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_device_claim_code_hash",
                schema: "devices",
                table: "device",
                column: "claim_code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_access_point_organization_id",
                schema: "devices",
                table: "device_access_point",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_device_access_point_access_point_id",
                schema: "devices",
                table: "device_access_point",
                column: "access_point_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_policy_organization_id",
                schema: "devices",
                table: "device_policy",
                column: "organization_id");

            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_device_site_name ON devices.device (site_id, lower(name));");

            foreach (var statement in DevicesIsolationSql.Up)
            {
                migrationBuilder.Sql(statement);
            }

            migrationBuilder.GrantAppAccess("devices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in DevicesIsolationSql.Down)
            {
                migrationBuilder.Sql(statement);
            }

            migrationBuilder.DropTable(
                name: "device_access_point",
                schema: "devices");

            migrationBuilder.DropTable(
                name: "device_policy",
                schema: "devices");

            migrationBuilder.DropTable(
                name: "device",
                schema: "devices");
        }
    }
}
