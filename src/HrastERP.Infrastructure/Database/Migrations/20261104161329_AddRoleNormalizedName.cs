using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrastERP.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleNormalizedName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Roles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows before the unique index is created (they'd all be '' otherwise).
            // Matches the app's Name.Trim().ToUpperInvariant() for the ASCII names in use.
            migrationBuilder.Sql("""
                UPDATE "Roles" SET "Name" = btrim("Name");
                UPDATE "Roles" SET "NormalizedName" = upper("Name");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NormalizedName",
                table: "Roles",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_NormalizedName",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Roles");
        }
    }
}
