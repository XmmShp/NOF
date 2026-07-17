using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class RefactorMessageArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_NOFInboxMessage",
                table: "NOFInboxMessage");

            migrationBuilder.DropColumn(
                name: "PayloadType",
                table: "NOFOutboxMessage");

            migrationBuilder.DropColumn(
                name: "HandlerType",
                table: "NOFInboxMessage");

            migrationBuilder.RenameColumn(
                name: "DispatchTypes",
                table: "NOFOutboxMessage",
                newName: "DispatchRoutes");

            migrationBuilder.RenameColumn(
                name: "PayloadType",
                table: "NOFInboxMessage",
                newName: "Route");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NOFInboxMessage",
                table: "NOFInboxMessage",
                columns: new[] { "Id", "Route" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_NOFInboxMessage",
                table: "NOFInboxMessage");

            migrationBuilder.RenameColumn(
                name: "DispatchRoutes",
                table: "NOFOutboxMessage",
                newName: "DispatchTypes");

            migrationBuilder.RenameColumn(
                name: "Route",
                table: "NOFInboxMessage",
                newName: "PayloadType");

            migrationBuilder.AddColumn<string>(
                name: "PayloadType",
                table: "NOFOutboxMessage",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HandlerType",
                table: "NOFInboxMessage",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NOFInboxMessage",
                table: "NOFInboxMessage",
                columns: new[] { "Id", "HandlerType" });
        }
    }
}
