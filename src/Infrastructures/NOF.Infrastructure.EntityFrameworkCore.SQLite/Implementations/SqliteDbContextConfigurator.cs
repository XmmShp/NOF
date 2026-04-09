using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NOF.Contract;
using System.Text.RegularExpressions;

namespace NOF.Infrastructure.EntityFrameworkCore.SQLite;

/// <summary>
/// SQLite database context configurator
/// Supports tenant isolation database naming strategy
/// </summary>
public class SqliteDbContextConfigurator : IDbContextConfigurator
{
    private readonly IConfiguration _configuration;
    private readonly SqliteOptions _options;
    private readonly TenantOptions _tenantOptions;
    private readonly Lazy<SqliteInMemoryConnectionKeeper> _connectionKeeper;

    public SqliteDbContextConfigurator(
        IConfiguration configuration,
        IOptions<SqliteOptions> options,
        IOptions<TenantOptions> tenantOptions,
        Lazy<SqliteInMemoryConnectionKeeper> connectionKeeperLazy)
    {
        _configuration = configuration;
        _options = options.Value;
        _tenantOptions = tenantOptions.Value;
        _connectionKeeper = connectionKeeperLazy;
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, string tenantId, TenantMode tenantMode)
    {
        var connectionString = _options.UseInMemory
            ? BuildInMemoryConnectionString(tenantId, tenantMode)
            : BuildConfiguredConnectionString(tenantId, tenantMode);

        optionsBuilder.UseSqlite(connectionString);
    }

    private string BuildConfiguredConnectionString(string tenantId, TenantMode tenantMode)
    {
        var connectionString = _configuration.GetConnectionString(_options.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"SQLite connection string '{_options.ConnectionStringName}' not found in configuration.");
        }

        if (tenantMode == TenantMode.DatabasePerTenant)
        {
            connectionString = BuildDatabasePerTenantConnectionString(connectionString, tenantId);
        }

        return connectionString;
    }

    private string BuildInMemoryConnectionString(string tenantId, TenantMode tenantMode)
    {
        var databaseName = tenantMode == TenantMode.DatabasePerTenant
            ? BuildDatabasePerTenantDatabaseName(_options.InMemoryDatabaseName, tenantId)
            : _options.InMemoryDatabaseName;

        return _connectionKeeper.Value.EnsureDatabase(databaseName);
    }

    private string BuildDatabasePerTenantConnectionString(string connectionString, string tenantId)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource) || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQLite Data Source must be a file path when TenantMode is DatabasePerTenant.");
        }

        var directory = Path.GetDirectoryName(dataSource);
        var baseFileName = Path.GetFileNameWithoutExtension(dataSource);
        var extension = Path.GetExtension(dataSource);
        var normalizedTenantId = NormalizeTenantIdForDatabaseName(tenantId);
        var tenantFileName = _tenantOptions.TenantDatabaseNameFormat
            .Replace("{database}", baseFileName, StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantId}", normalizedTenantId, StringComparison.OrdinalIgnoreCase);

        builder.DataSource = string.IsNullOrWhiteSpace(directory)
            ? $"{tenantFileName}{extension}"
            : Path.Combine(directory, $"{tenantFileName}{extension}");
        return builder.ToString();
    }

    private string BuildDatabasePerTenantDatabaseName(string databaseName, string tenantId)
    {
        var normalizedTenantId = NormalizeTenantIdForDatabaseName(tenantId);
        return _tenantOptions.TenantDatabaseNameFormat
            .Replace("{database}", databaseName, StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantId}", normalizedTenantId, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTenantIdForDatabaseName(string tenantId)
    {
        var normalized = NOFContractConstants.Tenant.NormalizeTenantId(tenantId).ToLowerInvariant();
        return Regex.Replace(normalized, "[^a-z0-9_]+", "_", RegexOptions.CultureInvariant);
    }
}
