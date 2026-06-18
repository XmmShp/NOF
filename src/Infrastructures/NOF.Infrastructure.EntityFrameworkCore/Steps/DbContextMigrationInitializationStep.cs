using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Hosting;

namespace NOF.Infrastructure;

internal sealed class DbContextMigrationInitializationStep(Type dbContextType) : IApplicationInitializationStep
{
    internal Type DbContextType { get; } = dbContextType;

    public TopologyComparison Compare(IApplicationInitializationStep other) => TopologyComparison.DoesNotMatter;

    public async Task ExecuteAsync(IHost app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(DbContextType);
        await dbContext.Database.MigrateAsync();
    }
}
