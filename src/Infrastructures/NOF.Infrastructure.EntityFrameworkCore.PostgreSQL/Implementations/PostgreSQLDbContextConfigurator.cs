using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Infrastructure;
using Npgsql;
using System.Text.RegularExpressions;

namespace NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;

/// <summary>
/// PostgreSQL database context configurator
/// Supports tenant isolation database naming strategy
/// </summary>
public class PostgreSQLDbContextConfigurator : IDbContextConfigurator
{
    private readonly IConfiguration _configuration;
    private readonly PostgreSQLOptions _options;
    private readonly TenantOptions _tenantOptions;

    public PostgreSQLDbContextConfigurator(IConfiguration configuration, IOptions<PostgreSQLOptions> options, IOptions<TenantOptions> tenantOptions)
    {
        _configuration = configuration;
        _options = options.Value;
        _tenantOptions = tenantOptions.Value;
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, string tenantId, TenantMode tenantMode)
    {
        var connectionString = _configuration.GetConnectionString(_options.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"PostgreSQL connection string '{_options.ConnectionStringName}' not found in configuration.");
        }

        if (tenantMode == TenantMode.DatabasePerTenant)
        {
            connectionString = BuildDatabasePerTenantConnectionString(connectionString, tenantId);
        }

        optionsBuilder.UseNpgsql(connectionString);
    }

    private string BuildDatabasePerTenantConnectionString(string connectionString, string tenantId)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var baseDatabaseName = builder.Database;

        if (string.IsNullOrWhiteSpace(baseDatabaseName))
        {
            throw new InvalidOperationException("PostgreSQL connection string must include a Database value when TenantMode is DatabasePerTenant.");
        }

        var normalizedTenantId = NormalizeTenantIdForDatabaseName(tenantId);
        builder.Database = _tenantOptions.TenantDatabaseNameFormat
            .Replace("{database}", baseDatabaseName, StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantId}", normalizedTenantId, StringComparison.OrdinalIgnoreCase);
        return builder.ConnectionString;
    }

    private static string NormalizeTenantIdForDatabaseName(string tenantId)
    {
        var normalized = NOFAbstractionConstants.Tenant.NormalizeTenantId(tenantId).ToLowerInvariant();
        return Regex.Replace(normalized, "[^a-z0-9_]+", "_", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
