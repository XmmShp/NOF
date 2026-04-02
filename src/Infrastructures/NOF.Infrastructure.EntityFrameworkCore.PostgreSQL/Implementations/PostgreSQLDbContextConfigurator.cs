using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;

/// <summary>
/// PostgreSQL database context configurator
/// Supports tenant isolation database naming strategy
/// </summary>
public class PostgreSQLDbContextConfigurator : IDbContextConfigurator
{
    private readonly IConfiguration _configuration;
    private readonly PostgreSQLOptions _options;

    public PostgreSQLDbContextConfigurator(IConfiguration configuration, IOptions<PostgreSQLOptions> options)
    {
        _configuration = configuration;
        _options = options.Value;
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, string tenantId)
    {
        var connectionString = _configuration.GetConnectionString(_options.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"PostgreSQL connection string '{_options.ConnectionStringName}' not found in configuration.");
        }

        optionsBuilder.UseNpgsql(connectionString);
    }
}
