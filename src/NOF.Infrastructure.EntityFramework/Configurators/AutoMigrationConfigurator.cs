using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public class AutoMigrationConfigurator : ISyncSeedConfigurator
{
    public async Task ExecuteAsync(StartupArgs args)
    {
        await using var scope = args.App.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
    }
}