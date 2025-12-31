using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class ModifyStateMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StateMachineContexts",
                table: "StateMachineContexts");

            migrationBuilder.AddColumn<string>(
                name: "DefinitionType",
                table: "StateMachineContexts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StateMachineContexts",
                table: "StateMachineContexts",
                columns: new[] { "CorrelationId", "DefinitionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StateMachineContexts",
                table: "StateMachineContexts");

            migrationBuilder.DropColumn(
                name: "DefinitionType",
                table: "StateMachineContexts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StateMachineContexts",
                table: "StateMachineContexts",
                column: "CorrelationId");
        }
    }
}
