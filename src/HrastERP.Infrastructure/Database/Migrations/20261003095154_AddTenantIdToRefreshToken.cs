using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrastERP.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RefreshToken",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TenantId",
                table: "RefreshToken",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_TenantId",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RefreshToken");
        }
    }
}
