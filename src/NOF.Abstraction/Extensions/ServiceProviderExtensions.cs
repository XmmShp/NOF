using NOF.Abstraction;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Helpers for activating scoped daemon services immediately after a new scope is created.
/// </summary>
public static class ServiceProviderExtensions
{
    extension(IServiceProvider services)
    {
        public IServiceProvider ResolveDaemonServices()
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.GetServices<IDaemonService>().ToArray();
            return services;
        }
    }
}
