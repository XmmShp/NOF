using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

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

    private readonly Lazy<HandlerInfos> _infos;

    public HandlerSelector(IServiceCollection services)
    {
        Services = services;
        _infos = new Lazy<HandlerInfos>(() => services.GetOrAddSingleton<HandlerInfos>());
    }

    /// <summary>
    /// Creates a <see cref="HandlerSelector"/> with a pre-resolved <see cref="HandlerInfos"/> instance.
    /// Used by source-generated code to avoid repeated <c>GetOrAddSingleton</c> lookups.
    /// </summary>
    public HandlerSelector(IServiceCollection services, HandlerInfos infos)
    {
        Services = services;
        _infos = new Lazy<HandlerInfos>(infos);
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
        _infos.Value.SetEndpointName(handlerType, endpointName);

        return this;
    }

    /// <summary>
    /// Registers state machine notification handlers from source-generated <see cref="StateMachineHandlerEntry"/> entries.
    /// This allows the Application-layer source generator to provide handler metadata
    /// without referencing <see cref="HandlerInfos"/>.
    /// </summary>
    /// <param name="entries">Array of entries from the generated static property.</param>
    /// <returns>This selector for further chaining.</returns>
    public HandlerSelector AddStateMachineHandlers(ReadOnlySpan<StateMachineHandlerEntry> entries)
    {
        var infos = _infos.Value;
        foreach (var entry in entries)
        {
            infos.Add(new NotificationHandlerInfo(entry.HandlerType, entry.NotificationType));
        }

        return this;
    }
}
