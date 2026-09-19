using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class DbContextMigrationInitializationStep(Type dbContextType) : IApplicationInitializationStep
{
    internal Type DbContextType { get; } = dbContextType;

    public TopologyComparison Compare(IApplicationInitializationStep other) => TopologyComparison.DoesNotMatter;

    public Task ExecuteAsync(IHost app)
        => MigrateTenantAsync(app.Services, NOFAbstractionConstants.Tenant.HostId,
            app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);

    internal async Task MigrateTenantAsync(IServiceProvider services, string tenantId, CancellationToken cancellationToken)
    {
        using var tenantContext = Context.PushCurrent(Context.Empty.WithTenantId(tenantId));
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(DbContextType);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
