using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.EntityFrameworkCore.SQLite;

/// <summary>
/// SQLite database context configurator
/// Supports tenant isolation database naming strategy
/// </summary>
public class SqliteDbContextConfigurator : IDbContextConfigurator
{
    private readonly IConfiguration _configuration;
    private readonly SqliteOptions _options;

    public SqliteDbContextConfigurator(IConfiguration configuration, IOptions<SqliteOptions> options)
    {
        _configuration = configuration;
        _options = options.Value;
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, string? tenantId)
    {
        // Get base connection string from configuration
        var connectionString = _configuration.GetConnectionString(_options.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"SQLite connection string '{_options.ConnectionStringName}' not found in configuration.");
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
