using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNOFTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NOFTenant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NOFTenant",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    __DeletedAtUnixTime = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOFTenant", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOFTenant___DeletedAtUnixTime",
                table: "NOFTenant",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_NOFTenant_Name",
                table: "NOFTenant",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NOFTenant_Name___DeletedAtUnixTime",
                table: "NOFTenant",
                columns: new[] { "Name", "__DeletedAtUnixTime" },
                unique: true);
        }
    }
}
