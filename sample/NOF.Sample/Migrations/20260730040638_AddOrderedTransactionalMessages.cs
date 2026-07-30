using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderedTransactionalMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CompletesOrderKey",
                table: "NOFOutboxMessage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OrderKey",
                table: "NOFOutboxMessage",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "NOFOutboxMessage",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompletesOrderKey",
                table: "NOFInboxMessage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OrderKey",
                table: "NOFInboxMessage",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "NOFInboxMessage",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NOFInboxOrderState",
                columns: table => new
                {
                    Route = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OrderKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    NextSequence = table.Column<long>(type: "bigint", nullable: false),
                    ClaimedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClaimExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BlockedSequence = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    __DeletedAtUnixTime = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFInboxOrderState", x => new { x.Route, x.OrderKey });
                });

            migrationBuilder.CreateTable(
                name: "NOFOutboxOrderState",
                columns: table => new
                {
                    OrderKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    CompletesOrderKey = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    __DeletedAtUnixTime = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFOutboxOrderState", x => new { x.OrderKey, x.Sequence });
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage_OrderKey_Sequence",
                table: "NOFOutboxMessage",
                columns: new[] { "OrderKey", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxMessage_Route_OrderKey_Sequence",
                table: "NOFInboxMessage",
                columns: new[] { "Route", "OrderKey", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxOrderState___DeletedAtUnixTime",
                table: "NOFInboxOrderState",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxOrderState_ClaimedBy_ClaimExpiresAtUtc",
                table: "NOFInboxOrderState",
                columns: new[] { "ClaimedBy", "ClaimExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxOrderState_UpdatedAtUtc",
                table: "NOFInboxOrderState",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxOrderState___DeletedAtUnixTime",
                table: "NOFOutboxOrderState",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxOrderState_CreatedAtUtc",
                table: "NOFOutboxOrderState",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NOFInboxOrderState");

            migrationBuilder.DropTable(
                name: "NOFOutboxOrderState");

            migrationBuilder.DropIndex(
                name: "IX_NOFOutboxMessage_OrderKey_Sequence",
                table: "NOFOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_NOFInboxMessage_Route_OrderKey_Sequence",
                table: "NOFInboxMessage");

            migrationBuilder.DropColumn(
                name: "CompletesOrderKey",
                table: "NOFOutboxMessage");

            migrationBuilder.DropColumn(
                name: "OrderKey",
                table: "NOFOutboxMessage");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "NOFOutboxMessage");

            migrationBuilder.DropColumn(
                name: "CompletesOrderKey",
                table: "NOFInboxMessage");

            migrationBuilder.DropColumn(
                name: "OrderKey",
                table: "NOFInboxMessage");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "NOFInboxMessage");
        }
    }
}
