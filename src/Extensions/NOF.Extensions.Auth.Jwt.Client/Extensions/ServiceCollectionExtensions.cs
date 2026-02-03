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

        return services;
    }
}