namespace NOF.Application;

/// <summary>
/// Provides convenience access to the ambient <see cref="IMapper"/> for the current async flow.
/// </summary>
/// <remarks>
/// The ambient mapper is bound by the current dependency injection scope. Use
/// <see cref="PushCurrent(IMapper)"/> to establish an explicit boundary in standalone code and tests.
/// </remarks>
public static class Mapper
{
    private static readonly AsyncLocal<IMapper?> _currentMapper = new();

    /// <summary>
    /// Gets the ambient <see cref="IMapper"/> for the current async flow.
    /// </summary>
    public static IMapper Current
        => _currentMapper.Value ?? throw new InvalidOperationException(
            "No ambient IMapper is available for the current async flow. " +
            "Resolve the scope's daemon services or use an explicit IMapper.");

    /// <summary>
    /// Pushes an ambient mapper and restores the previous mapper when the returned scope is disposed.
    /// </summary>
    public static IDisposable PushCurrent(IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        var previous = _currentMapper.Value;
        _currentMapper.Value = mapper;
        return new AmbientMapperScope(previous);
    }

    /// <summary>
    /// Resolves and pushes the current dependency injection scope's mapper.
    /// </summary>
    public static IDisposable PushCurrent(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var mapper = services.GetService(typeof(IMapper)) as IMapper
            ?? throw new InvalidOperationException($"No service of type '{typeof(IMapper).FullName}' is registered.");
        return PushCurrent(mapper);
    }

    private sealed class AmbientMapperScope(IMapper? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _currentMapper.Value = previous;
            _disposed = true;
        }
    }
}
