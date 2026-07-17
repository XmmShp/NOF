using NOF.Abstraction;

namespace NOF.Application;

public sealed class CommandHandlerRegistry : Registry<CommandHandlerRegistration>
{
    private readonly Dictionary<Type, List<Type>> _handlersByCommand = [];
    private readonly Dictionary<string, List<Type>> _handlersByCommandName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _commandTypesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _handlerTypesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _invokerTypesByHandlerName = new(StringComparer.Ordinal);

    public CommandHandlerRegistry()
    {
        Frozen += BuildIndexes;
    }

    public IReadOnlyCollection<Type> GetHandlers(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        Freeze();
        return _handlersByCommand.TryGetValue(commandType, out var handlers) ? handlers : Array.Empty<Type>();
    }

    public IReadOnlyCollection<Type> GetHandlers(string commandTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandTypeName);
        Freeze();
        return _handlersByCommandName.TryGetValue(commandTypeName, out var handlers) ? handlers : Array.Empty<Type>();
    }

    public bool TryGetCommandType(string commandTypeName, out Type commandType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandTypeName);
        Freeze();
        return _commandTypesByName.TryGetValue(commandTypeName, out commandType!);
    }

    public bool TryGetHandlerType(string handlerTypeName, out Type handlerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerTypeName);
        Freeze();
        return _handlerTypesByName.TryGetValue(handlerTypeName, out handlerType!);
    }

    public bool TryGetInvokerType(string handlerTypeName, out Type invokerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerTypeName);
        Freeze();
        return _invokerTypesByHandlerName.TryGetValue(handlerTypeName, out invokerType!);
    }

    private void BuildIndexes()
    {
        _handlersByCommand.Clear();
        _handlersByCommandName.Clear();
        _commandTypesByName.Clear();
        _handlerTypesByName.Clear();
        _invokerTypesByHandlerName.Clear();

        foreach (var registration in Items)
        {
            Index(registration);
        }
    }

    private void Index(CommandHandlerRegistration registration)
    {
        if (!_handlersByCommand.TryGetValue(registration.CommandType, out var handlers))
        {
            handlers = [];
            _handlersByCommand[registration.CommandType] = handlers;
        }

        if (!handlers.Contains(registration.HandlerType))
        {
            handlers.Add(registration.HandlerType);
        }

        if (!_handlersByCommandName.TryGetValue(registration.CommandTypeName, out var handlersByName))
        {
            handlersByName = [];
            _handlersByCommandName[registration.CommandTypeName] = handlersByName;
        }

        if (!handlersByName.Contains(registration.HandlerType))
        {
            handlersByName.Add(registration.HandlerType);
        }

        _commandTypesByName.TryAdd(registration.CommandTypeName, registration.CommandType);
        _handlerTypesByName.TryAdd(registration.HandlerTypeName, registration.HandlerType);
        _invokerTypesByHandlerName.TryAdd(registration.HandlerTypeName, registration.InvokerType);
    }
}
