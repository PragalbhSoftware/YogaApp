using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreferredCityAndNearestCityCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE PlatformSettings
                SET NearestCityMaxKm = 500
                WHERE NearestCityMaxKm > 500;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredCityId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PreferredCityId",
                table: "Users",
                column: "PreferredCityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Cities_PreferredCityId",
                table: "Users",
                column: "PreferredCityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Cities_PreferredCityId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PreferredCityId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredCityId",
                table: "Users");
        }
    }
}
