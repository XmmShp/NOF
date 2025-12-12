using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public class AutoMigrateConfigurator : ISyncSeedConfigurator
{
    public async Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        await using var scope = webApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
    }
}