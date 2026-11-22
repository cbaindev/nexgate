using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexgate.Common.BuildingBlocks.Persistence;

#nullable disable

namespace Nexgate.Data.Occupancy.Migrations
{
    /// <inheritdoc />
    public partial class InitialOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "occupancy");

            migrationBuilder.CreateTable(
                name: "subject",
                schema: "occupancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subject_organization_id",
                schema: "occupancy",
                table: "subject",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_site_id",
                schema: "occupancy",
                table: "subject",
                column: "site_id");

            migrationBuilder.EnableTenantIsolation("occupancy", "subject");
            migrationBuilder.GrantAppAccess("occupancy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subject",
                schema: "occupancy");
        }
    }
}
