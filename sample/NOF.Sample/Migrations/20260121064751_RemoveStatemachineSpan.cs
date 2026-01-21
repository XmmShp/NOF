using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStatemachineSpan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpanId",
                table: "StateMachineContextInfo");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "StateMachineContextInfo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpanId",
                table: "StateMachineContextInfo",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "StateMachineContextInfo",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
