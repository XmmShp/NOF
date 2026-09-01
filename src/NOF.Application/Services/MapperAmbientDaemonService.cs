using NOF.Abstraction;

namespace NOF.Application;

/// <summary>
/// Binds the current dependency injection scope's mapper to the ambient async flow.
/// </summary>
public sealed class MapperAmbientDaemonService : IDaemonService, IDisposable
{
    private readonly IDisposable _scope;

    public MapperAmbientDaemonService(IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _scope = Mapper.PushCurrent(mapper);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
