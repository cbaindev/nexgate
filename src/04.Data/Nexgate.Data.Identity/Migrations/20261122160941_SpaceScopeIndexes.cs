using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexgate.Data.Identity.Migrations
{
    /// <inheritdoc />
    public partial class SpaceScopeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_role_assignment_scope",
                schema: "identity",
                table: "role_assignment",
                columns: new[] { "scope_type", "scope_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_scope",
                schema: "identity",
                table: "invitation",
                columns: new[] { "scope_type", "scope_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_role_assignment_scope",
                schema: "identity",
                table: "role_assignment");

            migrationBuilder.DropIndex(
                name: "ix_invitation_scope",
                schema: "identity",
                table: "invitation");
        }
    }
}
