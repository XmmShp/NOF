using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class RevokedRefreshTokenPersistenceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    public ValueTask ExecuteAsync(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddScoped<IRevokedRefreshTokenRepository, PersistenceRevokedRefreshTokenRepository>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, RevokedRefreshTokenModelCreatingContributor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RevokedRefreshTokenCleanupBackgroundService>());

        return ValueTask.CompletedTask;
    }
}
