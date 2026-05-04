using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class NOFv1030 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "NOFTenant",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "NOFTenant",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "SentAt",
                table: "NOFOutboxMessage",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "FailedAt",
                table: "NOFOutboxMessage",
                newName: "FailedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "NOFOutboxMessage",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ClaimExpiresAt",
                table: "NOFOutboxMessage",
                newName: "ClaimExpiresAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_NOFOutboxMessage_Status_CreatedAt",
                table: "NOFOutboxMessage",
                newName: "IX_NOFOutboxMessage_Status_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_NOFOutboxMessage_Status_ClaimExpiresAt",
                table: "NOFOutboxMessage",
                newName: "IX_NOFOutboxMessage_Status_ClaimExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "ProcessedAt",
                table: "NOFInboxMessage",
                newName: "ProcessedAtUtc");

            migrationBuilder.RenameColumn(
                name: "FailedAt",
                table: "NOFInboxMessage",
                newName: "FailedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "NOFInboxMessage",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ClaimExpiresAt",
                table: "NOFInboxMessage",
                newName: "ClaimExpiresAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_NOFInboxMessage_Status_CreatedAt",
                table: "NOFInboxMessage",
                newName: "IX_NOFInboxMessage_Status_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_NOFInboxMessage_Status_ClaimExpiresAt",
                table: "NOFInboxMessage",
                newName: "IX_NOFInboxMessage_Status_ClaimExpiresAtUtc");

            migrationBuilder.CreateTable(
                name: "PersistedSigningKey",
                columns: table => new
                {
                    Kid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EncryptedPrivateKey = table.Column<string>(type: "text", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvalidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersistedSigningKey", x => x.Kid);
                });

            migrationBuilder.CreateTable(
                name: "RevokedRefreshToken",
                columns: table => new
                {
                    TokenId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevokedRefreshToken", x => x.TokenId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersistedSigningKey_Status_CreatedAtUtc",
                table: "PersistedSigningKey",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PersistedSigningKey_Status_InvalidatedAtUtc",
                table: "PersistedSigningKey",
                columns: new[] { "Status", "InvalidatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RevokedRefreshToken_ExpiresAtUtc",
                table: "RevokedRefreshToken",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersistedSigningKey");

            migrationBuilder.DropTable(
                name: "RevokedRefreshToken");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "NOFTenant",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "NOFTenant",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "NOFOutboxMessage",
                newName: "SentAt");

            migrationBuilder.RenameColumn(
                name: "FailedAtUtc",
                table: "NOFOutboxMessage",
                newName: "FailedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "NOFOutboxMessage",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "ClaimExpiresAtUtc",
                table: "NOFOutboxMessage",
                newName: "ClaimExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_NOFOutboxMessage_Status_CreatedAtUtc",
                table: "NOFOutboxMessage",
                newName: "IX_NOFOutboxMessage_Status_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_NOFOutboxMessage_Status_ClaimExpiresAtUtc",
                table: "NOFOutboxMessage",
                newName: "IX_NOFOutboxMessage_Status_ClaimExpiresAt");

            migrationBuilder.RenameColumn(
                name: "ProcessedAtUtc",
                table: "NOFInboxMessage",
                newName: "ProcessedAt");

            migrationBuilder.RenameColumn(
                name: "FailedAtUtc",
                table: "NOFInboxMessage",
                newName: "FailedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "NOFInboxMessage",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "ClaimExpiresAtUtc",
                table: "NOFInboxMessage",
                newName: "ClaimExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_NOFInboxMessage_Status_CreatedAtUtc",
                table: "NOFInboxMessage",
                newName: "IX_NOFInboxMessage_Status_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_NOFInboxMessage_Status_ClaimExpiresAtUtc",
                table: "NOFInboxMessage",
                newName: "IX_NOFInboxMessage_Status_ClaimExpiresAt");
        }
    }
}
