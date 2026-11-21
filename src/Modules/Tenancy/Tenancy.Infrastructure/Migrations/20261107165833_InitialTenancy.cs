using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexgate.BuildingBlocks.Persistence;

#nullable disable

namespace Nexgate.Modules.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "organization",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "site",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_organization",
                        column: x => x.organization_id,
                        principalSchema: "tenancy",
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_site_organization_id",
                schema: "tenancy",
                table: "site",
                column: "organization_id");

            // Nombre de sitio único por organización, sin distinguir mayúsculas.
            migrationBuilder.Sql(
                $"CREATE UNIQUE INDEX {TenancyDbContext.SiteNameIndex} ON tenancy.site (organization_id, lower(name));");

            // Aislamiento (design, decisión 4). La organización es la raíz: se filtra por su id, y solo se puede
            // insertar como operación de plataforma. Sin política de DELETE: no se borran organizaciones.
            migrationBuilder.Sql("ALTER TABLE tenancy.organization ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenancy.organization FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "CREATE POLICY organization_select ON tenancy.organization FOR SELECT " +
                "USING (id = public.nexgate_current_org());");
            migrationBuilder.Sql(
                "CREATE POLICY organization_update ON tenancy.organization FOR UPDATE " +
                "USING (id = public.nexgate_current_org()) WITH CHECK (id = public.nexgate_current_org());");
            migrationBuilder.Sql(
                "CREATE POLICY organization_insert ON tenancy.organization FOR INSERT " +
                $"WITH CHECK (current_setting('{DatabaseRoles.PlatformOperationSetting}', true) = 'on');");

            migrationBuilder.EnableTenantIsolation("tenancy", "site");
            migrationBuilder.GrantAppAccess("tenancy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "organization",
                schema: "tenancy");
        }
    }
}
