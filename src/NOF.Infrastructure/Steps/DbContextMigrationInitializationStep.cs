using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure;

internal sealed class DbContextMigrationInitializationStep(Type dbContextType) : IDatabaseMigrationInitializationStep
{
    internal Type DbContextType { get; } = dbContextType;

    public async Task ExecuteAsync(IHost app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(DbContextType);
        await dbContext.Database.MigrateAsync();
    }
}
