namespace NOF.Domain;

/// <summary>
/// Provides access to the ambient <see cref="IIdGenerator"/> for the current async flow.
/// </summary>
public static class IdGenerator
{
    private static readonly AsyncLocal<IIdGenerator?> _currentGenerator = new();

    /// <summary>
    /// Gets the ambient <see cref="IIdGenerator"/> instance for the current async flow.
    /// </summary>
    public static IIdGenerator Current
    {
        get => _currentGenerator.Value ?? throw new InvalidOperationException(
            "No ambient IIdGenerator is available for the current async flow.");
    }

    public static IDisposable PushCurrent(IIdGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        var previous = _currentGenerator.Value;
        _currentGenerator.Value = generator;
        return new AmbientIdGeneratorScope(previous);
    }

    public static IDisposable PushCurrent(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var generator = services.GetService(typeof(IIdGenerator)) as IIdGenerator
            ?? throw new InvalidOperationException($"No service of type '{typeof(IIdGenerator).FullName}' is registered.");
        return PushCurrent(generator);
    }

    private sealed class AmbientIdGeneratorScope : IDisposable
    {
        private readonly IIdGenerator? _previousGenerator;
        private bool _disposed;

        public AmbientIdGeneratorScope(IIdGenerator? previousGenerator)
        {
            _previousGenerator = previousGenerator;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _currentGenerator.Value = _previousGenerator;
            _disposed = true;
        }
    }
}
