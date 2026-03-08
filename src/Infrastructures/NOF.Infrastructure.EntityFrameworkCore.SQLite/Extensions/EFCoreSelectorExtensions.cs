using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.EntityFrameworkCore.SQLite;

public static class NOFInfrastructureEntityFrameworkCoreSQLiteExtensions
{
    extension(EFCoreSelector selector)
    {
        public INOFAppBuilder UseSqlite(string connectStringName = "sqlite")
        {
            // Register SQLite database context configurator (overrides default)
            selector.Builder.Services.ReplaceOrAddScoped<IDbContextConfigurator, SqliteDbContextConfigurator>();

            return selector.Builder;
        }
    }
}
