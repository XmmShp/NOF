using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Returned by the source-generated <c>AddAllHandlers</c> method.
/// Provides fluent API for handler registration.
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
