using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RefactorEfCoreMultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "EFCoreTenant");
        }
    }
}
