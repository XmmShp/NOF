using NOF.Abstraction;

namespace NOF.Domain;

/// <summary>
/// Activates the ambient <see cref="IIdGenerator"/> for the current dependency injection scope.
/// </summary>
/// <remarks>
/// This is part of NOF's convenience API support. Explicit <see cref="IIdGenerator"/>
/// dependencies remain the primary runtime contract.
/// </remarks>
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
