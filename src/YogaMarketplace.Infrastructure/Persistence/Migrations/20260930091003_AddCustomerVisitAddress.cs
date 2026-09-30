using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerVisitAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeArea",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeCity",
                table: "Users",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeLandmark",
                table: "Users",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeLine1",
                table: "Users",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomePin",
                table: "Users",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeArea",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HomeCity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HomeLandmark",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HomeLine1",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HomePin",
                table: "Users");
        }
    }
}
