using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrastERP.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TenantNormalizedName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_name",
                schema: "administration",
                table: "tenants");

            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                schema: "administration",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows the same way Tenant.UpdateName does: trimmed name, upper-cased normalized name
            migrationBuilder.Sql("UPDATE administration.tenants SET name = btrim(name), normalized_name = upper(btrim(name));");

            // The '' default only exists so AddColumn works on existing rows; the model has no default, and keeping it
            // would let a raw insert that omits normalized_name store '' instead of failing.
            migrationBuilder.Sql("ALTER TABLE administration.tenants ALTER COLUMN normalized_name DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_normalized_name",
                schema: "administration",
                table: "tenants",
                column: "normalized_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_normalized_name",
                schema: "administration",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                schema: "administration",
                table: "tenants");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_name",
                schema: "administration",
                table: "tenants",
                column: "name",
                unique: true);
        }
    }
}
