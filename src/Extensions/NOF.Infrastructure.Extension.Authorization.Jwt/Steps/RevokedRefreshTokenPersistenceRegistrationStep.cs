using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Hosting;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class RevokedRefreshTokenPersistenceRegistrationStep : IServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped<IRevokedRefreshTokenRepository, PersistenceRevokedRefreshTokenRepository>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INOFDbContextModelCreatingContributor, RevokedRefreshTokenModelCreatingContributor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RevokedRefreshTokenCleanupBackgroundService>());

        return ValueTask.CompletedTask;
    }
}
