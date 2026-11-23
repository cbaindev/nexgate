using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexgate.Common.BuildingBlocks.Persistence;

#nullable disable

namespace Nexgate.Data.Events.Migrations
{
    /// <inheritdoc />
    public partial class InitialEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "events");

            migrationBuilder.CreateTable(
                name: "access_event",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    credential_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_version = table.Column<int>(type: "integer", nullable: true),
                    uses_settled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_event", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_event_organization_id",
                schema: "events",
                table: "access_event",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_event_site_occurred",
                schema: "events",
                table: "access_event",
                columns: new[] { "site_id", "occurred_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_access_event_device_event",
                schema: "events",
                table: "access_event",
                columns: new[] { "device_id", "device_event_id" },
                unique: true);

            migrationBuilder.EnableTenantIsolation("events", "access_event");
            migrationBuilder.GrantAppAccess("events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_event",
                schema: "events");
        }
    }
}
