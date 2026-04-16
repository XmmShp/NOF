using NOF.Abstraction;

namespace NOF.Application;

public sealed class CommandHandlerInfos
{
    private readonly Lock _gate = new();
    private readonly HashSet<CommandHandlerRegistration> _registrations = [];
    private readonly Dictionary<Type, List<Type>> _handlersByCommand = new();
    private bool _isFrozen;

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

        lock (_gate)
        {
            ThrowIfFrozen();
            AddCore(registration);
        }
    }

    public void AddRange(ReadOnlySpan<CommandHandlerRegistration> registrations)
    {
        lock (_gate)
        {
            ThrowIfFrozen();
            foreach (var registration in registrations)
            {
                AddCore(registration);
            }
        }
    }

    public void Freeze()
    {
        EnsureInitialized();
    }

    public IReadOnlyCollection<Type> GetHandlers(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        EnsureInitialized();
        return _handlersByCommand.TryGetValue(commandType, out var handlers) ? handlers : Array.Empty<Type>();
    }

    private void EnsureInitialized()
    {
        if (_isFrozen)
        {
            return;
        }

        lock (_gate)
        {
            if (_isFrozen)
            {
                return;
            }

            foreach (var registration in Registry.CommandHandlerRegistrations)
            {
                AddCore(registration);
            }

            _isFrozen = true;
        }
    }

    private void AddCore(CommandHandlerRegistration registration)
    {
        _registrations.Add(registration);
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

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("CommandHandlerInfos is frozen after its first read.");
        }
    }
}
