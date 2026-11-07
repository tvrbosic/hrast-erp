using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrastERP.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAuditAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "asp_net_users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                table: "asp_net_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "asp_net_users",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_users");
        }
    }
}
