using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadersToOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing UUID Id column and recreate as bigint identity
            migrationBuilder.DropPrimaryKey(
                name: "PK_EFCoreOutboxMessage",
                table: "EFCoreOutboxMessage");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "EFCoreOutboxMessage");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "EFCoreOutboxMessage",
                type: "bigint",
                nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EFCoreOutboxMessage",
                table: "EFCoreOutboxMessage",
                column: "Id");

            migrationBuilder.AddColumn<string>(
                name: "Headers",
                table: "EFCoreOutboxMessage",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Headers",
                table: "EFCoreOutboxMessage");

            // Drop the bigint Id column and recreate as UUID
            migrationBuilder.DropPrimaryKey(
                name: "PK_EFCoreOutboxMessage",
                table: "EFCoreOutboxMessage");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "EFCoreOutboxMessage");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "EFCoreOutboxMessage",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_EFCoreOutboxMessage",
                table: "EFCoreOutboxMessage",
                column: "Id");
        }
    }
}
