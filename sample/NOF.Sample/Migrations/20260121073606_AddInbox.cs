using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StateMachineContextInfo");

            migrationBuilder.DropTable(
                name: "TransactionalMessage");

            migrationBuilder.CreateTable(
                name: "EFCoreInboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFCoreInboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EFCoreOutboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    PayloadType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    DestinationEndpointName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ClaimedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SpanId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFCoreOutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EFCoreStateMachineContext",
                columns: table => new
                {
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    DefinitionType = table.Column<string>(type: "text", nullable: false),
                    ContextType = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ContextData = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFCoreStateMachineContext", x => new { x.CorrelationId, x.DefinitionType });
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreInboxMessage_Status_CreatedAt",
                table: "EFCoreInboxMessage",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreInboxMessage_Status_ProcessedAt",
                table: "EFCoreInboxMessage",
                columns: new[] { "Status", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreOutboxMessage_ClaimedBy",
                table: "EFCoreOutboxMessage",
                column: "ClaimedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreOutboxMessage_Status_ClaimExpiresAt",
                table: "EFCoreOutboxMessage",
                columns: new[] { "Status", "ClaimExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreOutboxMessage_Status_CreatedAt",
                table: "EFCoreOutboxMessage",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreOutboxMessage_TraceId",
                table: "EFCoreOutboxMessage",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFCoreInboxMessage");

            migrationBuilder.DropTable(
                name: "EFCoreOutboxMessage");

            migrationBuilder.DropTable(
                name: "EFCoreStateMachineContext");

            migrationBuilder.CreateTable(
                name: "StateMachineContextInfo",
                columns: table => new
                {
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    DefinitionType = table.Column<string>(type: "text", nullable: false),
                    ContextData = table.Column<string>(type: "text", nullable: false),
                    ContextType = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateMachineContextInfo", x => new { x.CorrelationId, x.DefinitionType });
                });

            migrationBuilder.CreateTable(
                name: "TransactionalMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DestinationEndpointName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    PayloadType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SpanId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionalMessage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalMessage_ClaimedBy",
                table: "TransactionalMessage",
                column: "ClaimedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalMessage_Status_ClaimExpiresAt",
                table: "TransactionalMessage",
                columns: new[] { "Status", "ClaimExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalMessage_Status_CreatedAt",
                table: "TransactionalMessage",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalMessage_TraceId",
                table: "TransactionalMessage",
                column: "TraceId");
        }
    }
}
