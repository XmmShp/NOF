using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Hosting;

namespace NOF.Infrastructure.Extension.Authentication;

public sealed class RevokedRefreshTokenPersistenceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    public ValueTask ExecuteAsync(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddScoped<IRevokedRefreshTokenRepository, PersistenceRevokedRefreshTokenRepository>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INOFDbContextModelCreatingContributor, RevokedRefreshTokenModelCreatingContributor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RevokedRefreshTokenCleanupBackgroundService>());

        return ValueTask.CompletedTask;
    }
}
