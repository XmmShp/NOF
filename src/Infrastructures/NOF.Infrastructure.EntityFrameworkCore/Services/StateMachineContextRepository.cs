using System.Text.Json;

namespace NOF;

public class StateMachineContextRepository<TDatabase> : IStateMachineContextRepository
    where TDatabase : NOFDbContext
{
    private readonly TDatabase _dbContext;
    public StateMachineContextRepository(TDatabase dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<(IStateMachineContext Context, int State)?> FindAsync(string correlationId, Type definitionType)
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
        if (type is null || !typeof(IStateMachineContext).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Invalid context type: {dbEntity.ContextType}");
        }

        if (JsonSerializer.Deserialize(dbEntity.ContextData, type, JsonSerializerOptions.NOFDefaults) is not IStateMachineContext context)
        {
            return null;
        }
        return (context, dbEntity.State);
    }

    public void Add(string correlationId, Type definitionType, IStateMachineContext context, int state)
    {
        ArgumentNullException.ThrowIfNull(definitionType);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var definitionTypeString = definitionType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeString);

        var contextType = context.GetType();
        var contextTypeString = contextType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(contextTypeString);
        var contextData = JsonSerializer.Serialize(context, contextType, JsonSerializerOptions.NOFDefaults);

        var dbEntity = new StateMachineContextInfo
        {
            CorrelationId = correlationId,
            DefinitionType = definitionTypeString,
            ContextType = contextTypeString,
            ContextData = contextData,
            State = state
        };

        _dbContext.StateMachineContexts.Add(dbEntity);
    }

    public void Update(string correlationId, Type definitionType, IStateMachineContext context, int state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(definitionType);

        var definitionTypeString = definitionType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeString);

        var dbEntity = _dbContext.StateMachineContexts.Find(correlationId, definitionTypeString);

        if (dbEntity is null)
        {
            return;
        }

        var type = context.GetType();
        if (type.AssemblyQualifiedName != dbEntity.ContextType)
        {
            throw new InvalidOperationException($"Invalid context type: {type.AssemblyQualifiedName}");
        }

        var contextData = JsonSerializer.Serialize(context, type, JsonSerializerOptions.NOFDefaults);
        if (contextData != dbEntity.ContextData)
        {
            dbEntity.ContextData = contextData;
        }

        if (state != dbEntity.State)
        {
            dbEntity.State = state;
        }
    }
}
