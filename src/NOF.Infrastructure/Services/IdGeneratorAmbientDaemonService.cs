using NOF.Abstraction;
using NOF.Domain;

namespace NOF.Infrastructure;

public sealed class IdGeneratorAmbientDaemonService : IDaemonService, IDisposable
{
    private readonly IDisposable _scope;

    public IdGeneratorAmbientDaemonService(IIdGenerator generator)
    {
        _scope = IdGenerator.PushCurrent(generator);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
