namespace NOF.Application;

/// <summary>
/// Persistence port for state machine contexts.
/// This is intentionally not a generic repository: it only exposes operations needed by the state machine runtime.
/// </summary>
public interface IStateMachineContextStore
{
    ValueTask<NOFStateMachineContext?> FindAsync(string correlationId, string definitionTypeName, CancellationToken cancellationToken = default);

    void Add(NOFStateMachineContext context);
}

