using NOF.Annotation;

namespace NOF.Abstraction;

/// <summary>
/// Runtime view of source-generated AutoInject registrations.
/// First read imports from <see cref="Registry"/> and then freezes.
/// </summary>
public sealed class AutoInjectInfos
{
    private readonly Lock _initializeGate = new();
    private readonly Registry _registry;
    private readonly FreezableList<AutoInjectServiceRegistration> _registrations = [];
    private bool _isInitialized;

    public AutoInjectInfos()
        : this(new Registry())
    {
    }

    public AutoInjectInfos(Registry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<AutoInjectServiceRegistration> Registrations
    {
        get
        {
            EnsureInitialized();
            return _registrations;
        }
    }

    public void Add(AutoInjectServiceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registrations.Add(registration);
    }

    public void AddRange(IEnumerable<AutoInjectServiceRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _registrations.AddRange(registrations);
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

            _registrations.AddRange(_registry.AutoInjectRegistrations);
            _registrations.Freeze();
            _isInitialized = true;
        }
    }
}
