using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "RevokedRefreshToken",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "PersistedSigningKey",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "NOFTenant",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "NOFStateMachineContext",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "NOFOutboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "NOFInboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "ConfigNodeChildren",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "__DeletedAtUtc",
                table: "ConfigNode",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevokedRefreshToken___DeletedAtUtc",
                table: "RevokedRefreshToken",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedSigningKey___DeletedAtUtc",
                table: "PersistedSigningKey",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFTenant___DeletedAtUtc",
                table: "NOFTenant",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFStateMachineContext___DeletedAtUtc",
                table: "NOFStateMachineContext",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage___DeletedAtUtc",
                table: "NOFOutboxMessage",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxMessage___DeletedAtUtc",
                table: "NOFInboxMessage",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNodeChildren___DeletedAtUtc",
                table: "ConfigNodeChildren",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode___DeletedAtUtc",
                table: "ConfigNode",
                column: "__DeletedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RevokedRefreshToken___DeletedAtUtc",
                table: "RevokedRefreshToken");

            migrationBuilder.DropIndex(
                name: "IX_PersistedSigningKey___DeletedAtUtc",
                table: "PersistedSigningKey");

            migrationBuilder.DropIndex(
                name: "IX_NOFTenant___DeletedAtUtc",
                table: "NOFTenant");

            migrationBuilder.DropIndex(
                name: "IX_NOFStateMachineContext___DeletedAtUtc",
                table: "NOFStateMachineContext");

            migrationBuilder.DropIndex(
                name: "IX_NOFOutboxMessage___DeletedAtUtc",
                table: "NOFOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_NOFInboxMessage___DeletedAtUtc",
                table: "NOFInboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNodeChildren___DeletedAtUtc",
                table: "ConfigNodeChildren");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNode___DeletedAtUtc",
                table: "ConfigNode");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "RevokedRefreshToken");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "PersistedSigningKey");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "NOFTenant");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "NOFStateMachineContext");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "NOFOutboxMessage");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "NOFInboxMessage");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "ConfigNodeChildren");

            migrationBuilder.DropColumn(
                name: "__DeletedAtUtc",
                table: "ConfigNode");
        }
    }
}
