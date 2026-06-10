using NOF.Abstraction;
using NOF.Application;

namespace NOF.Infrastructure;

public sealed class ContextAmbientDaemonService : IDaemonService, IDisposable
{
    private readonly IDisposable _scope;

    public ContextAmbientDaemonService(IContextAccessor accessor, NOFContext context)
    {
        _scope = AmbientContext.PushCurrent(accessor, context);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
