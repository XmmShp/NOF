using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NOF.Hosting;

namespace NOF.Infrastructure;

public static class NOFInfrastructureEntityFrameworkCoreSqLiteExtensions
{
    extension(EFCoreSelector selector)
    {
        public INOFAppBuilder UseSqlite(string connectStringName = "sqlite")
        {
            selector.Builder.Services.Configure<SqliteOptions>(options =>
            {
                options.ConnectionStringName = connectStringName;
                options.UseInMemory = false;
            });
            selector.Builder.Services.TryAddSingleton<SqliteInMemoryConnectionKeeper>();
            selector.Builder.Services.ReplaceOrAddSingleton(new DbContextOptionsConfiguration
            {
                Configure = static (sp, optionsBuilder, tenantId, tenantMode) =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var options = sp.GetRequiredService<IOptions<SqliteOptions>>().Value;
                    var tenantOptions = sp.GetRequiredService<IOptions<TenantOptions>>().Value;
                    var connectionKeeper = sp.GetRequiredService<SqliteInMemoryConnectionKeeper>();
                    var connectionString = options.UseInMemory
                        ? BuildInMemoryConnectionString(connectionKeeper, options, tenantOptions, tenantId, tenantMode)
                        : BuildConfiguredConnectionString(configuration, options, tenantOptions, tenantId, tenantMode);

                    optionsBuilder.UseSqlite(connectionString);
                }
            });

            return selector.Builder;
        }

        public INOFAppBuilder UseSqliteInMemory(string databaseName = "nof-sqlite-memory")
        {
            selector.Builder.Services.Configure<SqliteOptions>(options =>
            {
                options.UseInMemory = true;
                options.InMemoryDatabaseName = databaseName;
            });
            selector.Builder.Services.TryAddSingleton<SqliteInMemoryConnectionKeeper, SqliteInMemoryConnectionKeeper>();
            selector.Builder.Services.ReplaceOrAddSingleton(new DbContextOptionsConfiguration
            {
                Configure = static (sp, optionsBuilder, tenantId, tenantMode) =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var options = sp.GetRequiredService<IOptions<SqliteOptions>>().Value;
                    var tenantOptions = sp.GetRequiredService<IOptions<TenantOptions>>().Value;
                    var connectionKeeper = sp.GetRequiredService<SqliteInMemoryConnectionKeeper>();
                    var connectionString = options.UseInMemory
                        ? BuildInMemoryConnectionString(connectionKeeper, options, tenantOptions, tenantId, tenantMode)
                        : BuildConfiguredConnectionString(configuration, options, tenantOptions, tenantId, tenantMode);

                    optionsBuilder.UseSqlite(connectionString);
                }
            });

            return selector.Builder;
        }
    }

    private static string BuildConfiguredConnectionString(
        IConfiguration configuration,
        SqliteOptions options,
        TenantOptions tenantOptions,
        string tenantId,
        TenantMode tenantMode)
    {
        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"SQLite connection string '{options.ConnectionStringName}' not found in configuration.");
        }

        if (tenantMode == TenantMode.DatabasePerTenant)
        {
            connectionString = BuildDatabasePerTenantConnectionString(connectionString, tenantId, tenantOptions);
        }

        return connectionString;
    }

    private static string BuildInMemoryConnectionString(
        SqliteInMemoryConnectionKeeper connectionKeeper,
        SqliteOptions options,
        TenantOptions tenantOptions,
        string tenantId,
        TenantMode tenantMode)
    {
        var databaseName = tenantMode == TenantMode.DatabasePerTenant
            ? BuildDatabasePerTenantDatabaseName(options.InMemoryDatabaseName, tenantId, tenantOptions)
            : options.InMemoryDatabaseName;

        return connectionKeeper.EnsureDatabase(databaseName);
    }

    private static string BuildDatabasePerTenantConnectionString(string connectionString, string tenantId, TenantOptions tenantOptions)
    {
        var normalizedTenantId = TenantId.Normalize(tenantId);
        if (ContainsTenantIdPlaceholder(connectionString))
        {
            return ReplaceTenantIdPlaceholder(connectionString, normalizedTenantId);
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource) || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQLite Data Source must be a file path when TenantMode is DatabasePerTenant.");
        }

        var directory = Path.GetDirectoryName(dataSource);
        var baseFileName = Path.GetFileNameWithoutExtension(dataSource);
        var extension = Path.GetExtension(dataSource);
        var tenantFileName = tenantOptions.TenantDatabaseNameFormat
            .Replace("{database}", baseFileName, StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantId}", normalizedTenantId, StringComparison.OrdinalIgnoreCase);

        builder.DataSource = string.IsNullOrWhiteSpace(directory)
            ? $"{tenantFileName}{extension}"
            : Path.Combine(directory, $"{tenantFileName}{extension}");
        return builder.ToString();
    }

    private static string BuildDatabasePerTenantDatabaseName(string databaseName, string tenantId, TenantOptions tenantOptions)
    {
        var normalizedTenantId = TenantId.Normalize(tenantId);
        return tenantOptions.TenantDatabaseNameFormat
            .Replace("{database}", databaseName, StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantId}", normalizedTenantId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsTenantIdPlaceholder(string value)
        => value.Contains("{tenantId}", StringComparison.OrdinalIgnoreCase);

    private static string ReplaceTenantIdPlaceholder(string value, string tenantId)
        => value.Replace("{tenantId}", tenantId, StringComparison.OrdinalIgnoreCase);

}
