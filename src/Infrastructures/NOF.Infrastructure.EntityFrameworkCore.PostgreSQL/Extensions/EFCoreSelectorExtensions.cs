using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Hosting;
using Npgsql;

namespace NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;

public static class NOFInfrastructureEntityFrameworkCorePostgreSQLExtensions
{
    extension(EFCoreSelector selector)
    {
        public INOFAppBuilder UsePostgreSQL(string connectStringName = "postgres")
        {
            selector.Builder.Services.Configure<PostgreSQLOptions>(options => options.ConnectionStringName = connectStringName);
            selector.Builder.Services.ReplaceOrAddSingleton(new DbContextOptionsConfiguration
            {
                Configure = static (sp, optionsBuilder, tenantId, tenantMode) =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var options = sp.GetRequiredService<IOptions<PostgreSQLOptions>>().Value;
                    var tenantOptions = sp.GetRequiredService<IOptions<TenantOptions>>().Value;
                    var connectionString = BuildConnectionString(configuration, options, tenantOptions, tenantId, tenantMode);

                    optionsBuilder.UseNpgsql(connectionString);
                }
            });

            return selector.Builder;
        }
    }

    private static string BuildConnectionString(
        IConfiguration configuration,
        PostgreSQLOptions options,
        TenantOptions tenantOptions,
        string tenantId,
        TenantMode tenantMode)
    {
        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"PostgreSQL connection string '{options.ConnectionStringName}' not found in configuration.");
        }

        if (tenantMode == TenantMode.DatabasePerTenant)
        {
            connectionString = BuildDatabasePerTenantConnectionString(connectionString, tenantId, tenantOptions);
        }

        return connectionString;
    }

    private static string BuildDatabasePerTenantConnectionString(string connectionString, string tenantId, TenantOptions tenantOptions)
    {
        var normalizedTenantId = TenantId.Normalize(tenantId);
        if (ContainsTenantIdPlaceholder(connectionString))
        {
            return ReplaceTenantIdPlaceholder(connectionString, normalizedTenantId);
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var baseDatabaseName = builder.Database;

        if (string.IsNullOrWhiteSpace(baseDatabaseName))
        {
            throw new InvalidOperationException("PostgreSQL connection string must include a Database value when TenantMode is DatabasePerTenant.");
        }

        builder.Database = tenantOptions.TenantDatabaseNameFormat
            .Replace("{database}", baseDatabaseName, StringComparison.OrdinalIgnoreCase)
            .Replace("{tenantId}", normalizedTenantId, StringComparison.OrdinalIgnoreCase);
        return builder.ConnectionString;
    }

    private static bool ContainsTenantIdPlaceholder(string value)
        => value.Contains("{tenantId}", StringComparison.OrdinalIgnoreCase);

    private static string ReplaceTenantIdPlaceholder(string value, string tenantId)
        => value.Replace("{tenantId}", tenantId, StringComparison.OrdinalIgnoreCase);

}
