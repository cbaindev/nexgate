using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexgate.BuildingBlocks.Persistence;

#nullable disable

namespace Nexgate.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "invitation",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scope_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_account",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    platform_operator = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "membership",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership", x => x.id);
                    table.UniqueConstraint("ux_membership_organization_user", x => new { x.organization_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_membership_user",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_session",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_session", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_session_user",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_assignment",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scope_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_assignment_membership",
                        columns: x => new { x.organization_id, x.user_id },
                        principalSchema: "identity",
                        principalTable: "membership",
                        principalColumns: new[] { "organization_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_session",
                        column: x => x.session_id,
                        principalSchema: "identity",
                        principalTable: "refresh_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_organization_id",
                schema: "identity",
                table: "invitation",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_invitation_token_hash",
                schema: "identity",
                table: "invitation",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_membership_user_id",
                schema: "identity",
                table: "membership",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_session_user_id",
                schema: "identity",
                table: "refresh_session",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_session_id",
                schema: "identity",
                table: "refresh_token",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_token_token_hash",
                schema: "identity",
                table: "refresh_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_assignment_organization_user",
                schema: "identity",
                table: "role_assignment",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_user_account_normalized_email",
                schema: "identity",
                table: "user_account",
                column: "normalized_email",
                unique: true);

            // Aislamiento (design, decisiones 5 y 6): políticas RLS, funciones restringidas y permisos del rol app.
            foreach (var statement in IdentityIsolationSql.Up)
            {
                migrationBuilder.Sql(statement);
            }

            migrationBuilder.GrantAppAccess("identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in IdentityIsolationSql.Down)
            {
                migrationBuilder.Sql(statement);
            }

            migrationBuilder.DropTable(
                name: "invitation",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refresh_token",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_assignment",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refresh_session",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "membership",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_account",
                schema: "identity");
        }
    }
}
