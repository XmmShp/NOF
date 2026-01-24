using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NOF.Sample.Migrations.Public
{
    /// <inheritdoc />
    public partial class AddPublicDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    PayloadType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    DestinationEndpointName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Headers = table.Column<string>(type: "text", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFCoreInboxMessage");

            migrationBuilder.DropTable(
                name: "EFCoreOutboxMessage");
        }
    }
}
