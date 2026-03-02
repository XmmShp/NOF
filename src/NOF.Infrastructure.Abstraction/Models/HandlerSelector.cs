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

    public HandlerSelector(IServiceCollection services)
    {
        Services = services;
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
        Services.Configure<EndpointNameOptions>(endpointNameOptions =>
        {
            endpointNameOptions.Set(type, endpointName);
        });
        return this;
    }
}
