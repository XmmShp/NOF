using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF;

public class AutoMigrateInitializationStep : IDataSeedInitializationStep
{
    public async Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        
        var publicDbContext = scope.ServiceProvider.GetService<NOFPublicDbContext>();
        if (publicDbContext?.Database.IsRelational() == true)
        {
            await publicDbContext.Database.MigrateAsync();
        }
    }
}
