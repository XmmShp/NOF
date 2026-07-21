using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOF.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AlignNofSoftDeleteUnixTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RevokedRefreshToken___DeletedAtUtc",
                table: "RevokedRefreshToken");

            migrationBuilder.DropIndex(
                name: "IX_PersistedSigningKey___DeletedAtUtc",
                table: "PersistedSigningKey");

            migrationBuilder.DropIndex(
                name: "IX_OAuthClient___DeletedAtUtc",
                table: "OAuthClient");

            migrationBuilder.DropIndex(
                name: "IX_NOFTenant___DeletedAtUtc",
                table: "NOFTenant");

            migrationBuilder.DropIndex(
                name: "IX_NOFTenant_Name",
                table: "NOFTenant");

            migrationBuilder.DropIndex(
                name: "IX_NOFStateMachineContext___DeletedAtUtc",
                table: "NOFStateMachineContext");

            migrationBuilder.DropIndex(
                name: "IX_NOFOutboxMessage___DeletedAtUtc",
                table: "NOFOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_NOFInboxMessage___DeletedAtUtc",
                table: "NOFInboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNodeChildren___DeletedAtUtc",
                table: "ConfigNodeChildren");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNode___DeletedAtUtc",
                table: "ConfigNode");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNode_Name",
                table: "ConfigNode");

            ConvertSoftDeleteColumn("RevokedRefreshToken");
            ConvertSoftDeleteColumn("PersistedSigningKey");
            ConvertSoftDeleteColumn("OAuthClient");
            ConvertSoftDeleteColumn("NOFTenant");
            ConvertSoftDeleteColumn("NOFStateMachineContext");
            ConvertSoftDeleteColumn("NOFOutboxMessage");
            ConvertSoftDeleteColumn("NOFInboxMessage");
            ConvertSoftDeleteColumn("ConfigNodeChildren");
            ConvertSoftDeleteColumn("ConfigNode");

            migrationBuilder.CreateIndex(
                name: "IX_RevokedRefreshToken___DeletedAtUnixTime",
                table: "RevokedRefreshToken",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedSigningKey___DeletedAtUnixTime",
                table: "PersistedSigningKey",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthClient___DeletedAtUnixTime",
                table: "OAuthClient",
                column: "__DeletedAtUnixTime");

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

            migrationBuilder.CreateIndex(
                name: "IX_NOFStateMachineContext___DeletedAtUnixTime",
                table: "NOFStateMachineContext",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage___DeletedAtUnixTime",
                table: "NOFOutboxMessage",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxMessage___DeletedAtUnixTime",
                table: "NOFInboxMessage",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNodeChildren___DeletedAtUnixTime",
                table: "ConfigNodeChildren",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode___DeletedAtUnixTime",
                table: "ConfigNode",
                column: "__DeletedAtUnixTime");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode_Name",
                table: "ConfigNode",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode_Name___DeletedAtUnixTime",
                table: "ConfigNode",
                columns: new[] { "Name", "__DeletedAtUnixTime" },
                unique: true);

            void ConvertSoftDeleteColumn(string table)
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE ""{table}""
                    RENAME COLUMN ""__DeletedAtUtc"" TO ""__DeletedAtUnixTime"";

                    ALTER TABLE ""{table}""
                    ALTER COLUMN ""__DeletedAtUnixTime"" TYPE bigint
                    USING COALESCE(FLOOR(EXTRACT(EPOCH FROM ""__DeletedAtUnixTime""))::bigint, 0);

                    ALTER TABLE ""{table}""
                    ALTER COLUMN ""__DeletedAtUnixTime"" SET DEFAULT 0;

                    ALTER TABLE ""{table}""
                    ALTER COLUMN ""__DeletedAtUnixTime"" SET NOT NULL;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RevokedRefreshToken___DeletedAtUnixTime",
                table: "RevokedRefreshToken");

            migrationBuilder.DropIndex(
                name: "IX_PersistedSigningKey___DeletedAtUnixTime",
                table: "PersistedSigningKey");

            migrationBuilder.DropIndex(
                name: "IX_OAuthClient___DeletedAtUnixTime",
                table: "OAuthClient");

            migrationBuilder.DropIndex(
                name: "IX_NOFTenant___DeletedAtUnixTime",
                table: "NOFTenant");

            migrationBuilder.DropIndex(
                name: "IX_NOFTenant_Name",
                table: "NOFTenant");

            migrationBuilder.DropIndex(
                name: "IX_NOFTenant_Name___DeletedAtUnixTime",
                table: "NOFTenant");

            migrationBuilder.DropIndex(
                name: "IX_NOFStateMachineContext___DeletedAtUnixTime",
                table: "NOFStateMachineContext");

            migrationBuilder.DropIndex(
                name: "IX_NOFOutboxMessage___DeletedAtUnixTime",
                table: "NOFOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_NOFInboxMessage___DeletedAtUnixTime",
                table: "NOFInboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNodeChildren___DeletedAtUnixTime",
                table: "ConfigNodeChildren");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNode___DeletedAtUnixTime",
                table: "ConfigNode");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNode_Name",
                table: "ConfigNode");

            migrationBuilder.DropIndex(
                name: "IX_ConfigNode_Name___DeletedAtUnixTime",
                table: "ConfigNode");

            RevertSoftDeleteColumn("RevokedRefreshToken");
            RevertSoftDeleteColumn("PersistedSigningKey");
            RevertSoftDeleteColumn("OAuthClient");
            RevertSoftDeleteColumn("NOFTenant");
            RevertSoftDeleteColumn("NOFStateMachineContext");
            RevertSoftDeleteColumn("NOFOutboxMessage");
            RevertSoftDeleteColumn("NOFInboxMessage");
            RevertSoftDeleteColumn("ConfigNodeChildren");
            RevertSoftDeleteColumn("ConfigNode");

            migrationBuilder.CreateIndex(
                name: "IX_RevokedRefreshToken___DeletedAtUtc",
                table: "RevokedRefreshToken",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedSigningKey___DeletedAtUtc",
                table: "PersistedSigningKey",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthClient___DeletedAtUtc",
                table: "OAuthClient",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFTenant___DeletedAtUtc",
                table: "NOFTenant",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFTenant_Name",
                table: "NOFTenant",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NOFStateMachineContext___DeletedAtUtc",
                table: "NOFStateMachineContext",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFOutboxMessage___DeletedAtUtc",
                table: "NOFOutboxMessage",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NOFInboxMessage___DeletedAtUtc",
                table: "NOFInboxMessage",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNodeChildren___DeletedAtUtc",
                table: "ConfigNodeChildren",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode___DeletedAtUtc",
                table: "ConfigNode",
                column: "__DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigNode_Name",
                table: "ConfigNode",
                column: "Name",
                unique: true);

            void RevertSoftDeleteColumn(string table)
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE ""{table}""
                    ALTER COLUMN ""__DeletedAtUnixTime"" DROP DEFAULT;

                    ALTER TABLE ""{table}""
                    ALTER COLUMN ""__DeletedAtUnixTime"" DROP NOT NULL;

                    ALTER TABLE ""{table}""
                    ALTER COLUMN ""__DeletedAtUnixTime"" TYPE timestamp with time zone
                    USING CASE
                        WHEN ""__DeletedAtUnixTime"" = 0 THEN NULL
                        ELSE to_timestamp(""__DeletedAtUnixTime"")
                    END;

                    ALTER TABLE ""{table}""
                    RENAME COLUMN ""__DeletedAtUnixTime"" TO ""__DeletedAtUtc"";
                ");
            }
        }
    }
}
