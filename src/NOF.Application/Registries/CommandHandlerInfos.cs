using NOF.Abstraction;

namespace NOF.Application;

public sealed class CommandHandlerInfos
{
    private readonly Lock _initializeGate = new();
    private readonly Registry _registry;
    private readonly FreezableList<CommandHandlerRegistration> _registrations = [];
    private readonly Dictionary<Type, List<Type>> _handlersByCommand = new();
    private bool _isInitialized;

    public CommandHandlerInfos()
        : this(new Registry())
    {
    }

    public CommandHandlerInfos(Registry registry)
    {
        _registry = registry;
    }

    public IReadOnlyCollection<CommandHandlerRegistration> Registrations
    {
        get
        {
            EnsureInitialized();
            return _registrations;
        }
    }

    public void Add(CommandHandlerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        AddCore(registration);
    }

    public void AddRange(ReadOnlySpan<CommandHandlerRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            AddCore(registration);
        }
    }

    public IReadOnlyCollection<Type> GetHandlers(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        EnsureInitialized();
        return _handlersByCommand.TryGetValue(commandType, out var handlers) ? handlers : Array.Empty<Type>();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        lock (_initializeGate)
        {
            if (_isInitialized)
            {
                return;
            }

            foreach (var registration in _registry.CommandHandlerRegistrations)
            {
                AddCore(registration);
            }

            _registrations.Freeze();
            _isInitialized = true;
        }
    }

    private void AddCore(CommandHandlerRegistration registration)
    {
        _registrations.Add(registration);
        Index(registration);
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
