using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore.SQLite;

public static class NOFInfrastructureEntityFrameworkCoreSqLiteExtensions
{
    extension(EFCoreSelector selector)
    {
        public INOFAppBuilder UseSqlite(string connectStringName = "sqlite")
        {
            // Register SQLite database context configurator (overrides default)
            selector.Builder.Services.Configure<SqliteOptions>(options =>
            {
                options.ConnectionStringName = connectStringName;
                options.UseInMemory = false;
            });
            selector.Builder.Services.ReplaceOrAddSingleton<SqliteInMemoryConnectionKeeper, SqliteInMemoryConnectionKeeper>();
            selector.Builder.Services.ReplaceOrAddScoped<IDbContextConfigurator, SqliteDbContextConfigurator>();

            return selector.Builder;
        }

        public INOFAppBuilder UseSqliteInMemory(string databaseName = "nof-sqlite-memory")
        {
            selector.Builder.Services.Configure<SqliteOptions>(options =>
            {
                options.UseInMemory = true;
                options.InMemoryDatabaseName = databaseName;
            });
            selector.Builder.Services.ReplaceOrAddSingleton<SqliteInMemoryConnectionKeeper, SqliteInMemoryConnectionKeeper>();
            selector.Builder.Services.ReplaceOrAddScoped<IDbContextConfigurator, SqliteDbContextConfigurator>();

            return selector.Builder;
        }
    }
}
