using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NOF.Infrastructure.EntityFrameworkCore;
using Xunit;

namespace NOF.Infrastructure.Tests.Persistence;

public sealed class TenantDbContextFactoryExtensionsTests
{
    [Fact]
    public async Task MigrateAsync_ShouldMigrateExplicitTenantDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nof-migrator-{Guid.NewGuid():N}.db");

        try
        {
            var factory = new RecordingTenantDbContextFactory($"Data Source={databasePath};Pooling=False");

            await factory.MigrateAsync("tenant-a");

            Assert.Equal("tenant-a", factory.LastTenantId);
            await using var verificationContext = factory.CreateDbContext("verification");
            var appliedMigrations = await verificationContext.Database.GetAppliedMigrationsAsync();
            Assert.Contains(CreateMigratorMarkerTable.Id, appliedMigrations);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldRejectBlankTenantId()
    {
        var factory = new RecordingTenantDbContextFactory("Data Source=:memory:");

        await Assert.ThrowsAsync<ArgumentException>(() => factory.MigrateAsync(" "));
    }

    private sealed class RecordingTenantDbContextFactory(string connectionString)
        : ITenantDbContextFactory<MigratorTestDbContext>
    {
        public string? LastTenantId { get; private set; }

        public MigratorTestDbContext CreateDbContext()
            => CreateDbContext("host");

        public MigratorTestDbContext CreateDbContext(string tenantId)
        {
            LastTenantId = tenantId;
            var options = new DbContextOptionsBuilder<MigratorTestDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new MigratorTestDbContext(options);
        }
    }
}

public sealed class MigratorTestDbContext(DbContextOptions<MigratorTestDbContext> options)
    : NOFDbContext(options);

[DbContext(typeof(MigratorTestDbContext))]
[Migration(Id)]
public sealed class CreateMigratorMarkerTable : Migration
{
    public const string Id = "20260904000000_CreateMigratorMarkerTable";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MigratorMarker",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MigratorMarker", value => value.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("MigratorMarker");
    }
}
