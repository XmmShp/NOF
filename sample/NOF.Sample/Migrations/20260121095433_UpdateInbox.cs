using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFCoreInboxMessage_Status_CreatedAt",
                table: "EFCoreInboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_EFCoreInboxMessage_Status_ProcessedAt",
                table: "EFCoreInboxMessage");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "EFCoreInboxMessage");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "EFCoreInboxMessage");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "EFCoreInboxMessage");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "EFCoreInboxMessage");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "EFCoreInboxMessage");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "EFCoreInboxMessage");

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreInboxMessage_CreatedAt",
                table: "EFCoreInboxMessage",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFCoreInboxMessage_CreatedAt",
                table: "EFCoreInboxMessage");

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "EFCoreInboxMessage",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "EFCoreInboxMessage",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "EFCoreInboxMessage",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "EFCoreInboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "EFCoreInboxMessage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "EFCoreInboxMessage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreInboxMessage_Status_CreatedAt",
                table: "EFCoreInboxMessage",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreInboxMessage_Status_ProcessedAt",
                table: "EFCoreInboxMessage",
                columns: new[] { "Status", "ProcessedAt" });
        }
    }
}
