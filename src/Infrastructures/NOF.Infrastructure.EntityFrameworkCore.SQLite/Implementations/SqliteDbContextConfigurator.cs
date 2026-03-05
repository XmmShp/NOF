using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace NOF.Infrastructure.EntityFrameworkCore.SQLite;

/// <summary>
/// SQLite database context configurator
/// Supports tenant isolation database naming strategy
/// </summary>
public class SqliteDbContextConfigurator : IDbContextConfigurator
{
    private readonly IConfiguration _configuration;

    public SqliteDbContextConfigurator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, string? tenantId)
    {
        // Get base connection string from configuration
        var connectionString = _configuration.GetConnectionString("sqlite");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("SQLite connection string 'sqlite' not found in configuration.");
        }

        // If no tenant ID, use base connection string directly (Host environment)
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            optionsBuilder.UseSqlite(connectionString);
            return;
        }

        // Apply tenant isolation database naming strategy
        var connBuilder = new SqliteConnectionStringBuilder(connectionString);

        // For SQLite, we typically modify the database file path
        if (string.IsNullOrWhiteSpace(connBuilder.DataSource))
        {
            connBuilder.DataSource = tenantId;
        }
        else
        {
            // Extract directory and filename
            var directory = Path.GetDirectoryName(connBuilder.DataSource);
            var filename = Path.GetFileNameWithoutExtension(connBuilder.DataSource);
            var extension = Path.GetExtension(connBuilder.DataSource);

            var tenantFilename = string.IsNullOrEmpty(filename)
                ? tenantId
                : $"{filename}-{tenantId}";

            var tenantPath = string.IsNullOrEmpty(directory)
                ? $"{tenantFilename}{extension}"
                : Path.Combine(directory, $"{tenantFilename}{extension}");

            connBuilder.DataSource = tenantPath;
        }

        optionsBuilder.UseSqlite(connBuilder.ConnectionString);
    }
}
