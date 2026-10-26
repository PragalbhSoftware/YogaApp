using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AreaUnderCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Areas_City_Name",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Areas");

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "Areas",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Areas",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Areas",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pincode",
                table: "Areas",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Areas_CityId_Name",
                table: "Areas",
                columns: new[] { "CityId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Cities_CityId",
                table: "Areas",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Cities_CityId",
                table: "Areas");

            migrationBuilder.DropIndex(
                name: "IX_Areas_CityId_Name",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "Pincode",
                table: "Areas");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Areas",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_City_Name",
                table: "Areas",
                columns: new[] { "City", "Name" },
                unique: true);
        }
    }
}
