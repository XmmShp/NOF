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

/// <summary>
/// Resolves request handler keyed-service keys by message type, handler type,
/// or endpoint name. All lookups are O(1) dictionary-based.
/// </summary>
public interface IRequestHandlerResolver
{
    /// <summary>
    /// Resolves by request type and optional endpoint name (request without response).
    /// When <paramref name="endpointName"/> is <c>null</c>, returns the first registered handler.
    /// </summary>
    ResolvedHandler? ResolveRequest(Type requestType, string? endpointName = null);

    /// <summary>
    /// Resolves by request type and optional endpoint name (request with response).
    /// When <paramref name="endpointName"/> is <c>null</c>, returns the first registered handler.
    /// </summary>
    ResolvedHandler? ResolveRequestWithResponse(Type requestType, string? endpointName = null);

    /// <summary>
    /// Resolves a request-without-response key by handler type (O(1)).
    /// </summary>
    RequestHandlerKey? ResolveRequestByHandler(Type handlerType);

    /// <summary>
    /// Resolves a request-with-response key by handler type (O(1)).
    /// </summary>
    RequestWithResponseHandlerKey? ResolveRequestWithResponseByHandler(Type handlerType);
}

/// <inheritdoc cref="ICommandHandlerResolver"/>
public sealed class CommandHandlerResolver : ICommandHandlerResolver
{
    // messageType â†?(endpointName â†?resolved)
    private readonly Dictionary<Type, Dictionary<string, ResolvedHandler>> _byMessage = new();
    // messageType â†?first registered resolved (for null-endpoint fast path)
    private readonly Dictionary<Type, ResolvedHandler> _byMessageDefault = new();
    // handlerType â†?key
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

/// <inheritdoc cref="IRequestHandlerResolver"/>
public sealed class RequestHandlerResolver : IRequestHandlerResolver
{
    // requestType â†?(endpointName â†?resolved)
    private readonly Dictionary<Type, Dictionary<string, ResolvedHandler>> _reqByMessage = new();
    private readonly Dictionary<Type, ResolvedHandler> _reqByMessageDefault = new();
    private readonly Dictionary<Type, RequestHandlerKey> _reqByHandler = new();

    private readonly Dictionary<Type, Dictionary<string, ResolvedHandler>> _rwrByMessage = new();
    private readonly Dictionary<Type, ResolvedHandler> _rwrByMessageDefault = new();
    private readonly Dictionary<Type, RequestWithResponseHandlerKey> _rwrByHandler = new();

    public RequestHandlerResolver(HandlerInfos infos)
    {
        foreach (var info in infos.RequestsWithoutResponse)
        {
            var ep = infos.GetEndpointName(info.HandlerType);
            var key = RequestHandlerKey.Of(info.RequestType, ep);
            var resolved = new ResolvedHandler(info.HandlerType, key);

            if (!_reqByMessage.TryGetValue(info.RequestType, out var epMap))
            {
                epMap = new Dictionary<string, ResolvedHandler>(StringComparer.Ordinal);
                _reqByMessage[info.RequestType] = epMap;
            }
            epMap.TryAdd(ep, resolved);

            if (!infos.HasExplicitEndpointName(info.HandlerType))
            {
                _reqByMessageDefault.TryAdd(info.RequestType, resolved);
            }

            _reqByHandler.TryAdd(info.HandlerType, key);
        }

        foreach (var info in infos.RequestsWithResponse)
        {
            var ep = infos.GetEndpointName(info.HandlerType);
            var key = RequestWithResponseHandlerKey.Of(info.RequestType, ep);
            var resolved = new ResolvedHandler(info.HandlerType, key);

            if (!_rwrByMessage.TryGetValue(info.RequestType, out var epMap))
            {
                epMap = new Dictionary<string, ResolvedHandler>(StringComparer.Ordinal);
                _rwrByMessage[info.RequestType] = epMap;
            }
            epMap.TryAdd(ep, resolved);

            if (!infos.HasExplicitEndpointName(info.HandlerType))
            {
                _rwrByMessageDefault.TryAdd(info.RequestType, resolved);
            }

            _rwrByHandler.TryAdd(info.HandlerType, key);
        }
    }

    public ResolvedHandler? ResolveRequest(Type requestType, string? endpointName = null)
    {
        if (string.IsNullOrWhiteSpace(endpointName))
        {
            return _reqByMessageDefault.TryGetValue(requestType, out var r) ? r : null;
        }

        return _reqByMessage.TryGetValue(requestType, out var epMap) && epMap.TryGetValue(endpointName, out var resolved)
            ? resolved
            : null;
    }

    public ResolvedHandler? ResolveRequestWithResponse(Type requestType, string? endpointName = null)
    {
        if (string.IsNullOrWhiteSpace(endpointName))
        {
            return _rwrByMessageDefault.TryGetValue(requestType, out var r) ? r : null;
        }

        return _rwrByMessage.TryGetValue(requestType, out var epMap) && epMap.TryGetValue(endpointName, out var resolved)
            ? resolved
            : null;
    }

    public RequestHandlerKey? ResolveRequestByHandler(Type handlerType)
        => _reqByHandler.GetValueOrDefault(handlerType);

    public RequestWithResponseHandlerKey? ResolveRequestWithResponseByHandler(Type handlerType)
        => _rwrByHandler.GetValueOrDefault(handlerType);
}
