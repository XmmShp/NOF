using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF;

public class AutoMigrateConfig<THostApplication> : IDataSeedConfig<THostApplication>
    where THostApplication : class, IHost
{
    public async Task ExecuteAsync(INOFAppBuilder builder, THostApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
    }
}