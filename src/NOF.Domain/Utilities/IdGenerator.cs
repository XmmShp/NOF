namespace NOF.Domain;

/// <summary>
/// Provides convenience access to the ambient <see cref="IIdGenerator"/> for the current async flow.
/// </summary>
/// <remarks>
/// Prefer explicit <see cref="IIdGenerator"/> dependencies in core runtime paths.
/// The ambient generator exists as a convenience API for source-generated <c>New()</c> helpers and similar call sites.
/// </remarks>
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

    /// <summary>
    /// Pushes an ambient <see cref="IIdGenerator"/> into the current async flow for convenience API usage.
    /// </summary>
    public static IDisposable PushCurrent(IIdGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        var previous = _currentGenerator.Value;
        _currentGenerator.Value = generator;
        return new AmbientIdGeneratorScope(previous);
    }

    /// <summary>
    /// Resolves and pushes the current scope's <see cref="IIdGenerator"/> into the ambient async flow.
    /// </summary>
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
