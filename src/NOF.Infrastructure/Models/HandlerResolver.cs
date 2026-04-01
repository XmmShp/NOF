using NOF.Application;
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
/// Resolves command handler keyed-service keys by message type or handler type.
/// All lookups are O(1) dictionary-based.
/// </summary>
public interface ICommandHandlerResolver
{
    /// <summary>
    /// Resolves by message type.
    /// </summary>
    ResolvedHandler? Resolve(Type commandType);

    /// <summary>
    /// Resolves by handler type (O(1)). Used when the concrete handler type is known at compile time.
    /// </summary>
    CommandHandlerKey? ResolveByHandler(Type handlerType);
}

/// <inheritdoc cref="ICommandHandlerResolver"/>
public sealed class CommandHandlerResolver : ICommandHandlerResolver
{
    // messageType -> resolved
    private readonly Dictionary<Type, ResolvedHandler> _byMessage = new();
    // handlerType -> key
    private readonly Dictionary<Type, CommandHandlerKey> _byHandler = new();

    public CommandHandlerResolver(HandlerInfos infos)
    {
        foreach (var info in infos.Commands)
        {
            var key = CommandHandlerKey.Of(info.CommandType);
            var resolved = new ResolvedHandler(info.HandlerType, key);

            _byMessage.TryAdd(info.CommandType, resolved);
            _byHandler.TryAdd(info.HandlerType, key);
        }
    }

    public ResolvedHandler? Resolve(Type commandType)
    {
        return _byMessage.TryGetValue(commandType, out var r) ? r : null;
    }

    public CommandHandlerKey? ResolveByHandler(Type handlerType)
        => _byHandler.GetValueOrDefault(handlerType);
}
