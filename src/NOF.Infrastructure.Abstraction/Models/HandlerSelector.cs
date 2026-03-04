using Microsoft.Extensions.DependencyInjection;

namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Returned by the source-generated <c>AddAllHandlers</c> method.
/// Provides fluent API to override endpoint names for handler types.
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
    /// Overrides the endpoint name for a specific handler type.
    /// Updates the handler info, keyed service registration, and endpoint name map.
    /// </summary>
    public HandlerSelector SetEndpointName<THandler>(string endpointName)
        => SetEndpointName(typeof(THandler), endpointName);

    /// <summary>
    /// Overrides the endpoint name for a specific handler type.
    /// Updates the handler info, keyed service registration, and endpoint name map.
    /// </summary>
    public HandlerSelector SetEndpointName(Type handlerType, string endpointName)
    {
        Services.GetOrAddSingleton<CommandHandlerInfos>().SetEndpointName(handlerType, endpointName);
        Services.GetOrAddSingleton<RequestWithoutResponseHandlerInfos>().SetEndpointName(handlerType, endpointName);
        Services.GetOrAddSingleton<RequestWithResponseHandlerInfos>().SetEndpointName(handlerType, endpointName);

        return this;
    }
}
