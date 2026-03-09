using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AdaptNOFNewVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFCoreInboxMessage");

            migrationBuilder.DropTable(
                name: "EFCoreOutboxMessage");

            migrationBuilder.DropTable(
                name: "EFCoreStateMachineContext");

            migrationBuilder.DropTable(
                name: "EFCoreTenant");

            migrationBuilder.CreateTable(
                name: "NOFInboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFInboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NOFOutboxMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DestinationEndpointName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    PayloadType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ClaimedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SpanId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFOutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NOFStateMachineContext",
                columns: table => new
                {
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    DefinitionTypeName = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFStateMachineContext", x => new { x.CorrelationId, x.DefinitionTypeName });
                });

            migrationBuilder.CreateTable(
                name: "NOFTenant",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFTenant", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxMessage_CreatedAt",
                table: "NOFInboxMessage",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage_ClaimedBy",
                table: "NOFOutboxMessage",
                column: "ClaimedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage_Status_ClaimExpiresAt",
                table: "NOFOutboxMessage",
                columns: new[] { "Status", "ClaimExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage_Status_CreatedAt",
                table: "NOFOutboxMessage",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage_TraceId",
                table: "NOFOutboxMessage",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_NOFTenant_Name",
                table: "NOFTenant",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NOFInboxMessage");

            migrationBuilder.DropTable(
                name: "NOFOutboxMessage");

            migrationBuilder.DropTable(
                name: "NOFStateMachineContext");

            migrationBuilder.DropTable(
                name: "NOFTenant");

            migrationBuilder.CreateTable(
                name: "EFCoreInboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFCoreInboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EFCoreOutboxMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DestinationEndpointName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Headers = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_EFCoreOutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EFCoreStateMachineContext",
                columns: table => new
                {
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    DefinitionType = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFCoreStateMachineContext", x => new { x.CorrelationId, x.DefinitionType });
                });

            migrationBuilder.CreateTable(
                name: "EFCoreTenant",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFCoreTenant", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreInboxMessage_CreatedAt",
                table: "EFCoreInboxMessage",
                column: "CreatedAt");

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

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreTenant_Name",
                table: "EFCoreTenant",
                column: "Name",
                unique: true);
        }
    }
}
