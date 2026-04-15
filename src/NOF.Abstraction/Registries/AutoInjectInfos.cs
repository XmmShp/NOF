using NOF.Annotation;

namespace NOF.Abstraction;

/// <summary>
/// Runtime view of source-generated AutoInject registrations.
/// First read imports from <see cref="Registry"/> and then freezes.
/// </summary>
public sealed class AutoInjectInfos
{
    private readonly Lock _gate = new();
    private readonly List<AutoInjectServiceRegistration> _registrations = [];
    private bool _isFrozen;

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
        lock (_gate)
        {
            ThrowIfFrozen();
            _registrations.Add(registration);
        }
    }

    public void AddRange(IEnumerable<AutoInjectServiceRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        lock (_gate)
        {
            ThrowIfFrozen();
            _registrations.AddRange(registrations);
        }
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

            // Import from static registry storage and then freeze.
            _registrations.AddRange(Registry.AutoInjectRegistrations);
            _isFrozen = true;
        }
    }

    private void ThrowIfFrozen()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("AutoInjectInfos is frozen after its first read.");
        }
    }
}
