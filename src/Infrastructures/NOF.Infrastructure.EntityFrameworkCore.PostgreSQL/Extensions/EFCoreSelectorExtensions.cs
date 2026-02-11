namespace NOF;

public static partial class NOFInfrastructureEntityFrameworkCorePostgreSQLExtensions
{
    extension(IEFCoreSelector selector)
    {
        public INOFAppBuilder UsePostgreSQL(string connectStringName = "postgres")
        {
            // Register PostgreSQL database context configurator (overrides default)
            selector.Builder.Services.ReplaceOrAddScoped<IDbContextConfigurator, PostgreSQLDbContextConfigurator>();

            return selector.Builder;
        }
    }
}
