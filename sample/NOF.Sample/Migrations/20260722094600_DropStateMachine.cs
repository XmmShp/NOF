using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class DropStateMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NOFStateMachineContext");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NOFStateMachineContext",
                columns: table => new
                {
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    DefinitionTypeName = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    __DeletedAtUnixTime = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFStateMachineContext", x => new { x.CorrelationId, x.DefinitionTypeName });
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOFStateMachineContext___DeletedAtUnixTime",
                table: "NOFStateMachineContext",
                column: "__DeletedAtUnixTime");
        }
    }
}
