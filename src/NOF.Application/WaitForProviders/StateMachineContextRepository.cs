using NOF.Application.Internals;

namespace NOF;

public interface IStateMachineContextRepository
{
    ValueTask<(Type, IStateMachineContext)?> FindAsync(string correlationId);
    void Add(IStateMachineContext context);
    void Update(IStateMachineContext context);
}