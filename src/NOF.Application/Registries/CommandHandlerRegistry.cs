using NOF.Abstraction;

namespace NOF.Application;

public sealed class CommandHandlerRegistry : Registry<CommandHandlerRegistration>
{
    private readonly Dictionary<Type, List<Type>> _handlersByCommand = [];

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

    private void BuildIndexes()
    {
        _handlersByCommand.Clear();
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
    }
}
