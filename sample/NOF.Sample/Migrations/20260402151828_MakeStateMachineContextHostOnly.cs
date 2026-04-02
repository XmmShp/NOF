using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class MakeStateMachineContextHostOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_NOFStateMachineContext",
                table: "NOFStateMachineContext");

            migrationBuilder.DropIndex(
                name: "IX_NOFStateMachineContext_TenantId",
                table: "NOFStateMachineContext");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NOFStateMachineContext");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NOFStateMachineContext",
                table: "NOFStateMachineContext",
                columns: new[] { "CorrelationId", "DefinitionTypeName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_NOFStateMachineContext",
                table: "NOFStateMachineContext");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "NOFStateMachineContext",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NOFStateMachineContext",
                table: "NOFStateMachineContext",
                columns: new[] { "CorrelationId", "DefinitionTypeName", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_NOFStateMachineContext_TenantId",
                table: "NOFStateMachineContext",
                column: "TenantId");
        }
    }
}
