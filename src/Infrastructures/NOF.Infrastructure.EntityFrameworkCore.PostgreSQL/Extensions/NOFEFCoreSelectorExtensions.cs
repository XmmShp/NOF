using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace NOF;

public static partial class __NOF_Infrastructure_EntityFrameworkCore_PostgreSQL_Extensions__
{
    extension(INOFEFCoreSelector selector)
    {
        public INOFAppBuilder UsePostgreSQL(string connectStringName = "postgres")
        {
            selector.Builder.StartupEventChannel.Subscribe<DbContextConfigurating>(e =>
            {
                var connectionString = selector.Builder.Configuration.GetConnectionString(connectStringName);

                // For regular DbContext, apply tenant isolation
                if (!string.IsNullOrEmpty(e.TenantId))
                {
                    var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);

                    connBuilder.Database = string.IsNullOrEmpty(connBuilder.Database)
                        ? e.TenantId
                        : $"{connBuilder.Database}-{e.TenantId}";

                    connectionString = connBuilder.ConnectionString;
                }

                e.Options.UseNpgsql(connectionString);
            });

            selector.Builder.StartupEventChannel.Subscribe<PublicDbContextConfigurating>(e =>
            {
                var connectionString = selector.Builder.Configuration.GetConnectionString(connectStringName);

                // For public DbContext, use {Database}Public database
                var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);
                connBuilder.Database = string.IsNullOrEmpty(connBuilder.Database)
                    ? "Public"
                    : $"{connBuilder.Database}Public";
                connectionString = connBuilder.ConnectionString;

                e.Options.UseNpgsql(connectionString);
            });
            selector.Builder.UsedPostgreSQL = true;
            return selector.Builder;
        }
    }
}
