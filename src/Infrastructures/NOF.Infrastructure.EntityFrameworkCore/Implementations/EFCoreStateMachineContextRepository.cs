using NOF.Application;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal class EFCoreStateMachineContextRepository : IStateMachineContextRepository
{
    private readonly NOFDbContext _dbContext;

    public EFCoreStateMachineContextRepository(NOFDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<StateMachineContext?> FindAsync(string correlationId, Type definitionType)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(definitionType);
        var definitionTypeString = definitionType.AssemblyQualifiedName;
        if (string.IsNullOrWhiteSpace(definitionTypeString))
        {
            return null;
        }

        var dbEntity = await _dbContext.StateMachineContexts.FindAsync(correlationId, definitionTypeString);
        if (dbEntity is null)
        {
            return null;
        }

        return StateMachineContext.Create(
            correlationId: correlationId,
            definitionType: definitionType,
            state: dbEntity.State);
    }

    public void Add(StateMachineContext stateMachineContext)
    {
        ArgumentNullException.ThrowIfNull(stateMachineContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateMachineContext.CorrelationId);
        ArgumentNullException.ThrowIfNull(stateMachineContext.DefinitionType);

        var definitionTypeString = stateMachineContext.DefinitionType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeString);

        _dbContext.StateMachineContexts.Add(new EFCoreStateMachineContext
        {
            CorrelationId = stateMachineContext.CorrelationId,
            DefinitionType = definitionTypeString,
            State = stateMachineContext.State
        });
    }

    public void Update(StateMachineContext stateMachineContext)
    {
        ArgumentNullException.ThrowIfNull(stateMachineContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateMachineContext.CorrelationId);
        ArgumentNullException.ThrowIfNull(stateMachineContext.DefinitionType);

        var definitionTypeString = stateMachineContext.DefinitionType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeString);

        var dbEntity = _dbContext.StateMachineContexts.Find(stateMachineContext.CorrelationId, definitionTypeString);
        if (dbEntity is null)
        {
            return;
        }

        if (stateMachineContext.State != dbEntity.State)
        {
            dbEntity.State = stateMachineContext.State;
        }
    }
}
