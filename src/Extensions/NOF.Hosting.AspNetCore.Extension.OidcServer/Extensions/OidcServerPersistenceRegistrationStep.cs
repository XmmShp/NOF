using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OidcServerPersistenceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    public ValueTask ExecuteAsync(IHostApplicationBuilder builder)
    {
        builder.Services.ReplaceOrAddScoped<ISigningKeyService, PersistenceSigningKeyService>();
        builder.Services.TryAddScoped<IRevokedRefreshTokenRepository, PersistenceRevokedRefreshTokenRepository>();
        builder.Services.TryAddScoped<PersistenceOAuthClientService>();
        builder.Services.TryAddScoped<IOAuthClientManagementService>(static serviceProvider =>
            serviceProvider.GetRequiredService<PersistenceOAuthClientService>());

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, PersistedSigningKeyModelCreatingContributor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, RevokedRefreshTokenModelCreatingContributor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, OAuthClientModelCreatingContributor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RevokedRefreshTokenCleanupBackgroundService>());

        return ValueTask.CompletedTask;
    }
}
