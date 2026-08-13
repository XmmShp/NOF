using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthClientJsonWebKeySet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JsonWebKeySet",
                table: "OAuthClient",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JsonWebKeySet",
                table: "OAuthClient");
        }
    }
}
