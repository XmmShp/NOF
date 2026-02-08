namespace NOF;

public static partial class __NOF_Infrastructure_EntityFrameworkCore_PostgreSQL_Extensions__
{
    extension(INOFEFCoreSelector selector)
    {
        public INOFAppBuilder UsePostgreSQL(string connectStringName = "postgres")
        {
            // Register PostgreSQL database context configurator (overrides default)
            selector.Builder.Services.ReplaceOrAddScoped<IDbContextConfigurator, PostgreSQLDbContextConfigurator>();

            return selector.Builder;
        }
    }
}
