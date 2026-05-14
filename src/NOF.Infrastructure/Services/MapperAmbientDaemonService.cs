using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class MapperAmbientDaemonService : IDaemonService, IDisposable
{
    private readonly IDisposable _scope;

    public MapperAmbientDaemonService(IMapper mapper)
    {
        _scope = Mapper.PushCurrent(mapper);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
