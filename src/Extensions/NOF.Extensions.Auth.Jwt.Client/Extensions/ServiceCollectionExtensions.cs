using Microsoft.Extensions.DependencyInjection;
using NOF;

namespace NOF;

/// <summary>
/// Extension methods for registering JWT client services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JWT client services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJwtClient(this IServiceCollection services)
    {
        // Register JWT validation service
        services.AddSingleton<JwtValidationService>();

        // Register JWT client service
        services.AddSingleton<JwtClientService>();

        // Register JWT claims principal service
        services.AddSingleton<JwtClaimsPrincipalService>();

        // Register JWT client initializer (hosted service)
        services.AddHostedService<JwtClientInitializer>();

        // Register notification handler for token revocation
        services.AddSingleton<INotificationHandler<TokenRevokedNotification>, TokenRevokedNotificationHandler>();

        return services;
    }
}

/// <summary>
/// Handler for token revocation notifications.
/// </summary>
public class TokenRevokedNotificationHandler : INotificationHandler<TokenRevokedNotification>
{
    private readonly JwtClientService _jwtClientService;

    public TokenRevokedNotificationHandler(JwtClientService jwtClientService)
    {
        _jwtClientService = jwtClientService;
    }

    public Task HandleAsync(TokenRevokedNotification notification, CancellationToken cancellationToken = default)
    {
        return _jwtClientService.HandleTokenRevokedAsync(notification, cancellationToken);
    }
}
