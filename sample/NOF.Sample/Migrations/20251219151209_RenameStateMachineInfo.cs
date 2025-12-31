using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class RenameStateMachineInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StateMachineContexts",
                table: "StateMachineContexts");

            migrationBuilder.RenameTable(
                name: "StateMachineContexts",
                newName: "StateMachineContextInfo");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StateMachineContextInfo",
                table: "StateMachineContextInfo",
                columns: new[] { "CorrelationId", "DefinitionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StateMachineContextInfo",
                table: "StateMachineContextInfo");

            migrationBuilder.RenameTable(
                name: "StateMachineContextInfo",
                newName: "StateMachineContexts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StateMachineContexts",
                table: "StateMachineContexts",
                columns: new[] { "CorrelationId", "DefinitionType" });
        }
    }
}
