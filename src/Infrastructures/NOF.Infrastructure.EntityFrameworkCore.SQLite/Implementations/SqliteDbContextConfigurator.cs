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
        var connectionString = _configuration.GetConnectionString(_options.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"SQLite connection string '{_options.ConnectionStringName}' not found in configuration.");
        }

        optionsBuilder.UseSqlite(connectionString);
    }
}
