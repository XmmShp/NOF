using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddTracingAndSpanIdToOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpanId",
                table: "TransactionalMessage",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "TransactionalMessage",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalMessage_TraceId",
                table: "TransactionalMessage",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionalMessage_TraceId",
                table: "TransactionalMessage");

            migrationBuilder.DropColumn(
                name: "SpanId",
                table: "TransactionalMessage");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "TransactionalMessage");
        }
    }
}
