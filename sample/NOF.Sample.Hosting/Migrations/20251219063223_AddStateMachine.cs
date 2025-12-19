using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddStateMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StateMachineContexts",
                columns: table => new
                {
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    ContextType = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ContextData = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateMachineContexts", x => x.CorrelationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StateMachineContexts");
        }
    }
}
