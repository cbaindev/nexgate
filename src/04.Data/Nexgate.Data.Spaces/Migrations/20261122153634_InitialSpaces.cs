using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexgate.Common.BuildingBlocks.Persistence;

#nullable disable

namespace Nexgate.Data.Spaces.Migrations
{
    /// <inheritdoc />
    public partial class InitialSpaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spaces");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

            migrationBuilder.CreateTable(
                name: "space",
                schema: "spaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "ltree", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_common = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space", x => x.id);
                    table.UniqueConstraint("ak_space_site_id", x => new { x.site_id, x.id });
                    table.CheckConstraint("ck_space_depth", "nlevel(path) <= 11");
                    table.CheckConstraint("ck_space_root_is_site", "(parent_id IS NULL) = (type = 'site')");
                    table.ForeignKey(
                        name: "fk_space_parent",
                        columns: x => new { x.site_id, x.parent_id },
                        principalSchema: "spaces",
                        principalTable: "space",
                        principalColumns: new[] { "site_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "access_point",
                schema: "spaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_space_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fail_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_point", x => x.id);
                    table.CheckConstraint("ck_access_point_from_differs", "from_space_id IS NULL OR from_space_id <> to_space_id");
                    table.ForeignKey(
                        name: "fk_access_point_from_space",
                        columns: x => new { x.site_id, x.from_space_id },
                        principalSchema: "spaces",
                        principalTable: "space",
                        principalColumns: new[] { "site_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_access_point_to_space",
                        columns: x => new { x.site_id, x.to_space_id },
                        principalSchema: "spaces",
                        principalTable: "space",
                        principalColumns: new[] { "site_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_point_site_id_from_space_id",
                schema: "spaces",
                table: "access_point",
                columns: new[] { "site_id", "from_space_id" });

            migrationBuilder.CreateIndex(
                name: "IX_access_point_site_id_to_space_id",
                schema: "spaces",
                table: "access_point",
                columns: new[] { "site_id", "to_space_id" });

            migrationBuilder.CreateIndex(
                name: "ix_access_point_organization_id",
                schema: "spaces",
                table: "access_point",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_space_site_id_parent_id",
                schema: "spaces",
                table: "space",
                columns: new[] { "site_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_space_organization_id",
                schema: "spaces",
                table: "space",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_space_path",
                schema: "spaces",
                table: "space",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ux_space_site_root",
                schema: "spaces",
                table: "space",
                column: "site_id",
                unique: true,
                filter: "parent_id IS NULL");

            // Nombres únicos sin distinguir mayúsculas: entre hermanos y, para los puntos, en el sitio
            // (design de add-spaces-and-access-points, decisión 3). La raíz es única por sitio (índice anterior).
            migrationBuilder.Sql(
                $"CREATE UNIQUE INDEX {SpacesDbContext.SiblingNameIndex} ON spaces.space (parent_id, lower(name)) WHERE parent_id IS NOT NULL;");
            migrationBuilder.Sql(
                $"CREATE UNIQUE INDEX {SpacesDbContext.AccessPointNameIndex} ON spaces.access_point (site_id, lower(name));");

            migrationBuilder.EnableTenantIsolation("spaces", "space");
            migrationBuilder.EnableTenantIsolation("spaces", "access_point");
            migrationBuilder.GrantAppAccess("spaces");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_point",
                schema: "spaces");

            migrationBuilder.DropTable(
                name: "space",
                schema: "spaces");
        }
    }
}
