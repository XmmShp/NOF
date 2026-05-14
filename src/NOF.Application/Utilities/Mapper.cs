namespace NOF.Application;

/// <summary>
/// Provides access to the ambient <see cref="IMapper"/> for the current async flow.
/// </summary>
public static class Mapper
{
    private static readonly AsyncLocal<IMapper?> _currentMapper = new();

    /// <summary>
    /// Gets the ambient <see cref="IMapper"/> instance for the current async flow.
    /// </summary>
    public static IMapper Current
    {
        get => _currentMapper.Value ?? throw new InvalidOperationException(
            "No ambient IMapper is available for the current async flow.");
    }

    public static IDisposable PushCurrent(IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        var previous = _currentMapper.Value;
        _currentMapper.Value = mapper;
        return new AmbientMapperScope(previous);
    }

    public static IDisposable PushCurrent(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var mapper = services.GetService(typeof(IMapper)) as IMapper
            ?? throw new InvalidOperationException($"No service of type '{typeof(IMapper).FullName}' is registered.");
        return PushCurrent(mapper);
    }

    private sealed class AmbientMapperScope : IDisposable
    {
        private readonly IMapper? _previousMapper;
        private bool _disposed;

        public AmbientMapperScope(IMapper? previousMapper)
        {
            _previousMapper = previousMapper;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _currentMapper.Value = _previousMapper;
            _disposed = true;
        }
    }
}
