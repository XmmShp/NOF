using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

/// <summary>
/// Pairs a handler's concrete type with its keyed-service key so that
/// callers can resolve the handler without knowing its type at compile time.
/// </summary>
public readonly record struct ResolvedHandler(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    object Key);

/// <summary>
/// Resolves command handler keyed-service keys by message type, handler type,
/// or endpoint name. All lookups are O(1) dictionary-based.
/// </summary>
public interface ICommandHandlerResolver
{
    /// <summary>
    /// Resolves by message type and optional endpoint name.
    /// When <paramref name="endpointName"/> is <c>null</c>, returns the first registered handler.
    /// </summary>
    ResolvedHandler? Resolve(Type commandType, string? endpointName = null);

    /// <summary>
    /// Resolves by handler type (O(1)). Used when the concrete handler type is known at compile time.
    /// </summary>
    CommandHandlerKey? ResolveByHandler(Type handlerType);
}

/// <inheritdoc cref="ICommandHandlerResolver"/>
public sealed class CommandHandlerResolver : ICommandHandlerResolver
{
    // messageType -> (endpointName -> resolved)
    private readonly Dictionary<Type, Dictionary<string, ResolvedHandler>> _byMessage = new();
    // messageType -> first registered resolved (for null-endpoint fast path)
    private readonly Dictionary<Type, ResolvedHandler> _byMessageDefault = new();
    // handlerType -> key
    private readonly Dictionary<Type, CommandHandlerKey> _byHandler = new();

    public CommandHandlerResolver(HandlerInfos infos)
    {
        foreach (var info in infos.Commands)
        {
            var ep = infos.GetEndpointName(info.HandlerType);
            var key = CommandHandlerKey.Of(info.CommandType, ep);
            var resolved = new ResolvedHandler(info.HandlerType, key);

            if (!_byMessage.TryGetValue(info.CommandType, out var epMap))
            {
                epMap = new Dictionary<string, ResolvedHandler>(StringComparer.Ordinal);
                _byMessage[info.CommandType] = epMap;
            }
            epMap.TryAdd(ep, resolved);

            // Handlers with explicit endpoint name are NOT resolved by null endpoint name
            if (!infos.HasExplicitEndpointName(info.HandlerType))
            {
                _byMessageDefault.TryAdd(info.CommandType, resolved);
            }

            _byHandler.TryAdd(info.HandlerType, key);
        }
    }

    public ResolvedHandler? Resolve(Type commandType, string? endpointName = null)
    {
        if (string.IsNullOrWhiteSpace(endpointName))
        {
            return _byMessageDefault.TryGetValue(commandType, out var r) ? r : null;
        }

        return _byMessage.TryGetValue(commandType, out var epMap) && epMap.TryGetValue(endpointName, out var resolved)
            ? resolved
            : null;
    }

    public CommandHandlerKey? ResolveByHandler(Type handlerType)
        => _byHandler.GetValueOrDefault(handlerType);
}
