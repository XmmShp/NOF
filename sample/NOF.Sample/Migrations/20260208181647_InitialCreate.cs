using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    ActiveFileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
                    ChildrenIds = table.Column<List<long>>(type: "bigint[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigNodeChildren", x => x.NodeId);
                });

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

            migrationBuilder.CreateTable(
                name: "EFCoreTenant",
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
                    table.PrimaryKey("PK_EFCoreTenant", x => x.Id);
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
                column: "Name",
                unique: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigFile");

            migrationBuilder.DropTable(
                name: "ConfigNodeChildren");

            migrationBuilder.DropTable(
                name: "EFCoreInboxMessage");

            migrationBuilder.DropTable(
                name: "EFCoreOutboxMessage");

            migrationBuilder.DropTable(
                name: "EFCoreStateMachineContext");

            migrationBuilder.DropTable(
                name: "EFCoreTenant");

            migrationBuilder.DropTable(
                name: "ConfigNode");
        }
    }
}
