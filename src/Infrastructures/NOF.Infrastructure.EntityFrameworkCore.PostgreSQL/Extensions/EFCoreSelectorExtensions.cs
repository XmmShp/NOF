using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;
using NOF.Infrastructure;

namespace NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;

public static class NOFInfrastructureEntityFrameworkCorePostgreSQLExtensions
{
    extension(EFCoreSelector selector)
    {
        public INOFAppBuilder UsePostgreSQL(string connectStringName = "postgres")
        {
            // Register PostgreSQL database context configurator (overrides default)
            selector.Builder.Services.Configure<PostgreSQLOptions>(options => options.ConnectionStringName = connectStringName);
            selector.Builder.Services.ReplaceOrAddScoped<IDbContextConfigurator, PostgreSQLDbContextConfigurator>();

            return selector.Builder;
        }
    }
}
