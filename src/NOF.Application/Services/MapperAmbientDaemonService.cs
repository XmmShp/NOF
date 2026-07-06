using NOF.Abstraction;

namespace NOF.Application;

/// <summary>
/// Activates the ambient <see cref="IMapper"/> for the current dependency injection scope.
/// </summary>
/// <remarks>
/// This is part of NOF's convenience API support. Explicit <see cref="IMapper"/>
/// dependencies remain the primary runtime contract.
/// </remarks>
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
