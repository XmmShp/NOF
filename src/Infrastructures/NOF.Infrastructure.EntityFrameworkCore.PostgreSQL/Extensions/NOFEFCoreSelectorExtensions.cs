using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

                var tenantContext = e.ServiceProvider.GetService<ITenantContext>();
                if (tenantContext != null && !string.IsNullOrEmpty(tenantContext.CurrentTenantId))
                {
                    var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);

                    connBuilder.Database = string.IsNullOrEmpty(connBuilder.Database)
                        ? tenantContext.CurrentTenantId
                        : $"{connBuilder.Database}-{tenantContext.CurrentTenantId}";

                    connectionString = connBuilder.ConnectionString;
                }

                e.Options.UseNpgsql(connectionString);
            });
            selector.Builder.UsedPostgreSQL = true;
            return selector.Builder;
        }
    }
}
