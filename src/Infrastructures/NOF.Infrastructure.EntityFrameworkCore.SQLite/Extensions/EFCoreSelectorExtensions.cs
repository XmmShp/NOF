using NOF.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore.SQLite;

public static class NOFInfrastructureEntityFrameworkCoreSqLiteExtensions
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
