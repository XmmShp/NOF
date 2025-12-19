namespace NOF.Application.Dependents;

public interface IStateMachineContextRepository
{
    ValueTask<(IStateMachineContext Context, int State)?> FindAsync(string correlationId, Type definitionType);
    void Add(string correlationId, Type definitionType, IStateMachineContext context, int state);
    void Update(string correlationId, Type definitionType, IStateMachineContext context, int state);
}