using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;

/// <summary>
/// PostgreSQL database context configurator
/// Supports tenant isolation database naming strategy
/// </summary>
public class PostgreSQLDbContextConfigurator : IDbContextConfigurator
{
    private readonly IConfiguration _configuration;

    public PostgreSQLDbContextConfigurator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, string? tenantId)
    {
        // Get base connection string from configuration
        var connectionString = _configuration.GetConnectionString("postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string 'postgres' not found in configuration.");
        }

        // If no tenant ID, use base connection string directly (Host environment)
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            optionsBuilder.UseNpgsql(connectionString);
            return;
        }

        // Apply tenant isolation database naming strategy
        var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        connBuilder.Database = string.IsNullOrWhiteSpace(connBuilder.Database)
            ? tenantId
            : $"{connBuilder.Database}-{tenantId}";

        optionsBuilder.UseNpgsql(connBuilder.ConnectionString);
    }
}
