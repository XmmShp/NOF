using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.EntityFrameworkCore.PostgreSQL;

public static partial class NOFInfrastructureEntityFrameworkCorePostgreSQLExtensions
{
    extension(EFCoreSelector selector)
    {
        public INOFAppBuilder UsePostgreSQL(string connectStringName = "postgres")
        {
            // Register PostgreSQL database context configurator (overrides default)
            selector.Builder.Services.ReplaceOrAddScoped<IDbContextConfigurator, PostgreSQLDbContextConfigurator>();

            return selector.Builder;
        }
    }
}
