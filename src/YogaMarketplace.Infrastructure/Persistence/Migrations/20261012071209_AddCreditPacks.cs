using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YogaMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreditBookingStandsAfterExpiry",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreditConvenienceFee",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreditLateCancel",
                table: "PlatformSettings",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "PayInstructorShare");

            migrationBuilder.AddColumn<string>(
                name: "CreditPayBasis",
                table: "PlatformSettings",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "PackPerSession");

            migrationBuilder.AddColumn<decimal>(
                name: "PackReferenceSessionPrice",
                table: "PlatformSettings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 799m);

            migrationBuilder.AddColumn<decimal>(
                name: "PackRefundFee",
                table: "PlatformSettings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "PackRefundsEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SlotId",
                table: "CheckoutIntents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ServiceId",
                table: "CheckoutIntents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProviderId",
                table: "CheckoutIntents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "CheckoutIntents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "CheckoutIntents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Booking");

            migrationBuilder.AddColumn<Guid>(
                name: "PackId",
                table: "CheckoutIntents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesCredit",
                table: "CheckoutIntents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditBatchId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditValue",
                table: "Bookings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CreditAccounts",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditAccounts", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_CreditAccounts_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditPacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Sessions = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: false),
                    PriceCap = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckoutIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Sessions = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: false),
                    PriceCap = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ChargeConvenienceFee = table.Column<bool>(type: "bit", nullable: false),
                    PayBasis = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    LateCancel = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    BookingStandsAfterExpiry = table.Column<bool>(type: "bit", nullable: false),
                    RefundsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ReferenceSessionPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RefundFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PurchasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Remaining = table.Column<int>(type: "int", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RefundedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditBatches_CheckoutIntents_CheckoutIntentId",
                        column: x => x.CheckoutIntentId,
                        principalTable: "CheckoutIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditBatches_CreditPacks_PackId",
                        column: x => x.PackId,
                        principalTable: "CreditPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditBatches_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditLedger_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditLedger_CreditBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "CreditBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditLedger_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutIntents_GatewayPaymentId",
                table: "CheckoutIntents",
                column: "GatewayPaymentId",
                unique: true,
                filter: "[GatewayPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutIntents_PackId",
                table: "CheckoutIntents",
                column: "PackId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CreditBatchId",
                table: "Bookings",
                column: "CreditBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditBatches_CheckoutIntentId",
                table: "CreditBatches",
                column: "CheckoutIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditBatches_CustomerId_ExpiresAt",
                table: "CreditBatches",
                columns: new[] { "CustomerId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditBatches_GatewayPaymentId",
                table: "CreditBatches",
                column: "GatewayPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditBatches_PackId",
                table: "CreditBatches",
                column: "PackId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_BatchId_Kind",
                table: "CreditLedger",
                columns: new[] { "BatchId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_CustomerId",
                table: "CreditLedger",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_OneExpirePerBatch",
                table: "CreditLedger",
                column: "BatchId",
                unique: true,
                filter: "Kind = 'Expire'");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_OnePurchasePerBatch",
                table: "CreditLedger",
                column: "BatchId",
                unique: true,
                filter: "Kind = 'Purchase'");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_OneRefundPerBatch",
                table: "CreditLedger",
                column: "BatchId",
                unique: true,
                filter: "Kind = 'Refund'");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_OneReturnPerBooking",
                table: "CreditLedger",
                column: "BookingId",
                unique: true,
                filter: "Kind = 'Return'");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_OneUsePerBooking",
                table: "CreditLedger",
                column: "BookingId",
                unique: true,
                filter: "Kind = 'Use'");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPacks_IsActive",
                table: "CreditPacks",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_CreditBatches_CreditBatchId",
                table: "Bookings",
                column: "CreditBatchId",
                principalTable: "CreditBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckoutIntents_CreditPacks_PackId",
                table: "CheckoutIntents",
                column: "PackId",
                principalTable: "CreditPacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_CreditBatches_CreditBatchId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckoutIntents_CreditPacks_PackId",
                table: "CheckoutIntents");

            migrationBuilder.DropTable(
                name: "CreditAccounts");

            migrationBuilder.DropTable(
                name: "CreditLedger");

            migrationBuilder.DropTable(
                name: "CreditBatches");

            migrationBuilder.DropTable(
                name: "CreditPacks");

            // Pack checkouts have no provider, service or slot; they go before those columns become required again.
            migrationBuilder.Sql("EXEC(N'DELETE FROM CheckoutIntents WHERE Kind = ''Pack''')");

            migrationBuilder.DropIndex(
                name: "IX_CheckoutIntents_GatewayPaymentId",
                table: "CheckoutIntents");

            migrationBuilder.DropIndex(
                name: "IX_CheckoutIntents_PackId",
                table: "CheckoutIntents");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CreditBatchId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CreditBookingStandsAfterExpiry",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "CreditConvenienceFee",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "CreditLateCancel",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "CreditPayBasis",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PackReferenceSessionPrice",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PackRefundFee",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PackRefundsEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentId",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "PackId",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "UsesCredit",
                table: "CheckoutIntents");

            migrationBuilder.DropColumn(
                name: "CreditBatchId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CreditValue",
                table: "Bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "SlotId",
                table: "CheckoutIntents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ServiceId",
                table: "CheckoutIntents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProviderId",
                table: "CheckoutIntents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
