using System.Text.Json;

namespace NOF;

public class StateMachineContextRepository : IStateMachineContextRepository
{
    private readonly NOFDbContext _dbContext;
    public StateMachineContextRepository(NOFDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<StateMachineInfo?> FindAsync(string correlationId, Type definitionType)
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

        return StateMachineInfo.Create(
            correlationId: correlationId,
            definitionType: definitionType,
            context: context,
            state: dbEntity.State,
            traceId: dbEntity.TraceId,
            spanId: dbEntity.SpanId);
    }

    public void Add(StateMachineInfo stateMachineInfo)
    {
        ArgumentNullException.ThrowIfNull(stateMachineInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateMachineInfo.CorrelationId);
        ArgumentNullException.ThrowIfNull(stateMachineInfo.Context);
        ArgumentNullException.ThrowIfNull(stateMachineInfo.DefinitionType);

        var definitionTypeString = stateMachineInfo.DefinitionType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeString);

        var contextType = stateMachineInfo.Context.GetType();
        var contextTypeString = contextType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(contextTypeString);
        var contextData = JsonSerializer.Serialize(stateMachineInfo.Context, contextType, JsonSerializerOptions.NOFDefaults);

        var dbEntity = new StateMachineContextInfo
        {
            CorrelationId = stateMachineInfo.CorrelationId,
            DefinitionType = definitionTypeString,
            ContextType = contextTypeString,
            ContextData = contextData,
            State = stateMachineInfo.State,
            TraceId = stateMachineInfo.TraceId,
            SpanId = stateMachineInfo.SpanId
        };

        _dbContext.StateMachineContexts.Add(dbEntity);
    }

    public void Update(StateMachineInfo stateMachineInfo)
    {
        ArgumentNullException.ThrowIfNull(stateMachineInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateMachineInfo.CorrelationId);
        ArgumentNullException.ThrowIfNull(stateMachineInfo.DefinitionType);

        var definitionTypeString = stateMachineInfo.DefinitionType.AssemblyQualifiedName;
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionTypeString);

        var dbEntity = _dbContext.StateMachineContexts.Find(stateMachineInfo.CorrelationId, definitionTypeString);

        if (dbEntity is null)
        {
            return;
        }

        var type = stateMachineInfo.Context.GetType();
        if (type.AssemblyQualifiedName != dbEntity.ContextType)
        {
            throw new InvalidOperationException($"Invalid context type: {type.AssemblyQualifiedName}");
        }

        var contextData = JsonSerializer.Serialize(stateMachineInfo.Context, type, JsonSerializerOptions.NOFDefaults);
        if (contextData != dbEntity.ContextData)
        {
            dbEntity.ContextData = contextData;
        }

        if (stateMachineInfo.State != dbEntity.State)
        {
            dbEntity.State = stateMachineInfo.State;
        }

        // 更新追踪信息
        dbEntity.TraceId = stateMachineInfo.TraceId;
        dbEntity.SpanId = stateMachineInfo.SpanId;
    }
}
