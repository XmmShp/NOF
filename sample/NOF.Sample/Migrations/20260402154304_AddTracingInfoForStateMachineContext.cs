using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddTracingInfoForStateMachineContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TracingInfo_SpanId",
                table: "NOFStateMachineContext",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TracingInfo_TraceId",
                table: "NOFStateMachineContext",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TracingInfo_SpanId",
                table: "NOFStateMachineContext");

            migrationBuilder.DropColumn(
                name: "TracingInfo_TraceId",
                table: "NOFStateMachineContext");
        }
    }
}
