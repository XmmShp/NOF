using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddOidcServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OAuthClient",
                columns: table => new
                {
                    ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SecretSalt = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AllowedScopes = table.Column<string>(type: "text", nullable: false),
                    AccessTokenClaims = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    __DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthClient", x => x.ClientId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthClient___DeletedAtUtc",
                table: "OAuthClient",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthClient_IsEnabled",
                table: "OAuthClient",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuthClient");
        }
    }
}
