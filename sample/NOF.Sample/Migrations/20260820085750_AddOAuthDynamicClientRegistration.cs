using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthDynamicClientRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedGrantTypes",
                table: "OAuthClient",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AllowedResponseTypes",
                table: "OAuthClient",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationType",
                table: "OAuthClient",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationAccessTokenHash",
                table: "OAuthClient",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationAccessTokenSalt",
                table: "OAuthClient",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationMetadata",
                table: "OAuthClient",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TokenEndpointAuthenticationMethod",
                table: "OAuthClient",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedGrantTypes",
                table: "OAuthClient");

            migrationBuilder.DropColumn(
                name: "AllowedResponseTypes",
                table: "OAuthClient");

            migrationBuilder.DropColumn(
                name: "ApplicationType",
                table: "OAuthClient");

            migrationBuilder.DropColumn(
                name: "RegistrationAccessTokenHash",
                table: "OAuthClient");

            migrationBuilder.DropColumn(
                name: "RegistrationAccessTokenSalt",
                table: "OAuthClient");

            migrationBuilder.DropColumn(
                name: "RegistrationMetadata",
                table: "OAuthClient");

            migrationBuilder.DropColumn(
                name: "TokenEndpointAuthenticationMethod",
                table: "OAuthClient");
        }
    }
}
