using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigNode",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActiveFileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigNode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigNodeChildren",
                columns: table => new
                {
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    ChildrenIds = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigNodeChildren", x => x.NodeId);
                });

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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    PayloadType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ClaimedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClaimExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "ConfigFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigFile_ConfigNode_NodeId",
                        column: x => x.NodeId,
                        principalTable: "ConfigNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigFile_NodeId",
                table: "ConfigFile",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode_Name",
                table: "ConfigNode",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode_TenantId",
                table: "ConfigNode",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode_TenantId_Name",
                table: "ConfigNode",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNodeChildren_TenantId",
                table: "ConfigNodeChildren",
                column: "TenantId");

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
                name: "ConfigFile");

            migrationBuilder.DropTable(
                name: "ConfigNodeChildren");

            migrationBuilder.DropTable(
                name: "NOFInboxMessage");

            migrationBuilder.DropTable(
                name: "NOFOutboxMessage");

            migrationBuilder.DropTable(
                name: "NOFStateMachineContext");

            migrationBuilder.DropTable(
                name: "NOFTenant");

            migrationBuilder.DropTable(
                name: "ConfigNode");
        }
    }
}
