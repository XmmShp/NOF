using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Returned by the source-generated <c>AddAllHandlers</c> method.
/// Provides a fluent API to override endpoint names for handler and message types.
/// </summary>
public sealed class HandlerSelector
{
    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services { get; }
    private readonly Lazy<EndpointNameRegistry> _endpointNameRegistry;

    public HandlerSelector(IServiceCollection services)
    {
        Services = services;
        _endpointNameRegistry = new Lazy<EndpointNameRegistry>(
            () => Services.GetOrAddSingleton<EndpointNameRegistry>());
    }

    public HandlerSelector(IServiceCollection services, EndpointNameRegistry endpointNameRegistry)
    {
        Services = services;
        _endpointNameRegistry = new Lazy<EndpointNameRegistry>(endpointNameRegistry);
    }

    /// <summary>
    /// Overrides the endpoint name for the specified type.
    /// </summary>
    public HandlerSelector SetEndpointName<T>(string endpointName)
        => SetEndpointName(typeof(T), endpointName);

    /// <summary>
    /// Overrides the endpoint name for the specified type.
    /// </summary>
    public HandlerSelector SetEndpointName(Type type, string endpointName)
    {
        _endpointNameRegistry.Value.Set(type, endpointName);
        return this;
    }
}
