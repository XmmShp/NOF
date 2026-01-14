using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimsToOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimExpiresAt",
                table: "TransactionalMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                table: "TransactionalMessage",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalMessage_ClaimedBy",
                table: "TransactionalMessage",
                column: "ClaimedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalMessage_Status_ClaimExpiresAt",
                table: "TransactionalMessage",
                columns: new[] { "Status", "ClaimExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionalMessage_ClaimedBy",
                table: "TransactionalMessage");

            migrationBuilder.DropIndex(
                name: "IX_TransactionalMessage_Status_ClaimExpiresAt",
                table: "TransactionalMessage");

            migrationBuilder.DropColumn(
                name: "ClaimExpiresAt",
                table: "TransactionalMessage");

            migrationBuilder.DropColumn(
                name: "ClaimedBy",
                table: "TransactionalMessage");
        }
    }
}
