using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AdaptMultiTenantPattern : Migration
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

            migrationBuilder.CreateIndex(
                name: "IX_EFCoreInboxMessage_CreatedAt",
                table: "EFCoreInboxMessage",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFCoreInboxMessage");
        }
    }
}
