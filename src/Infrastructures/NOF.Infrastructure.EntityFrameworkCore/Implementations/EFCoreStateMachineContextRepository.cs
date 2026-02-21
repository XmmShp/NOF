using NOF.Application;
using NOF.Contract;
using System.Text.Json;

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

        var type = Type.GetType(dbEntity.ContextType, throwOnError: true);
        if (type is null)
        {
            throw new InvalidOperationException($"Invalid context type: {dbEntity.ContextType}");
        }

        if (JsonSerializer.Deserialize(dbEntity.ContextData, type, JsonSerializerOptions.NOFDefaults) is not { } context)
        {
            return null;
        }

        return StateMachineContext.Create(
            correlationId: correlationId,
            definitionType: definitionType,
            context: context,
            state: dbEntity.State);
    }

    public void Add(StateMachineContext stateMachineContext)
    {
        ArgumentNullException.ThrowIfNull(stateMachineContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateMachineContext.CorrelationId);
        ArgumentNullException.ThrowIfNull(stateMachineContext.Context);
        ArgumentNullException.ThrowIfNull(stateMachineContext.DefinitionType);

        var definitionTypeString = stateMachineContext.DefinitionType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeString);

        var contextType = stateMachineContext.Context.GetType();
        var contextTypeString = contextType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(contextTypeString);
        var contextData = JsonSerializer.Serialize(stateMachineContext.Context, contextType, JsonSerializerOptions.NOFDefaults);

        var dbEntity = new EFCoreStateMachineContext
        {
            CorrelationId = stateMachineContext.CorrelationId,
            DefinitionType = definitionTypeString,
            ContextType = contextTypeString,
            ContextData = contextData,
            State = stateMachineContext.State
        };

        _dbContext.StateMachineContexts.Add(dbEntity);
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

        var type = stateMachineContext.Context.GetType();
        if (type.AssemblyQualifiedName != dbEntity.ContextType)
        {
            throw new InvalidOperationException($"Invalid context type: {type.AssemblyQualifiedName}");
        }

        var contextData = JsonSerializer.Serialize(stateMachineContext.Context, type, JsonSerializerOptions.NOFDefaults);
        if (contextData != dbEntity.ContextData)
        {
            dbEntity.ContextData = contextData;
        }

        if (stateMachineContext.State != dbEntity.State)
        {
            dbEntity.State = stateMachineContext.State;
        }
    }
}
