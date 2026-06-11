using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class ContextAmbientDaemonService : IDaemonService, IDisposable
{
    private readonly IDisposable _scope;

    public ContextAmbientDaemonService(IContextAccessor accessor)
    {
        _scope = AmbientContext.PushCurrent(accessor, accessor.Context);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
