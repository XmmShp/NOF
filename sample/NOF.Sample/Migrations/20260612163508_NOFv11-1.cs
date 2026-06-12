using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class NOFv111 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpanId",
                table: "NOFOutboxMessage");

            migrationBuilder.RenameColumn(
                name: "TraceId",
                table: "NOFOutboxMessage",
                newName: "TraceParent");

            migrationBuilder.RenameIndex(
                name: "IX_NOFOutboxMessage_TraceId",
                table: "NOFOutboxMessage",
                newName: "IX_NOFOutboxMessage_TraceParent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TraceParent",
                table: "NOFOutboxMessage",
                newName: "TraceId");

            migrationBuilder.RenameIndex(
                name: "IX_NOFOutboxMessage_TraceParent",
                table: "NOFOutboxMessage",
                newName: "IX_NOFOutboxMessage_TraceId");

            migrationBuilder.AddColumn<string>(
                name: "SpanId",
                table: "NOFOutboxMessage",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
