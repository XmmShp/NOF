using NOF.Application.Internals;
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

    public async ValueTask<(Type, IStateMachineContext)?> FindAsync(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        var dbEntity = await _dbContext.StateMachineContexts.FindAsync(correlationId);

        if (dbEntity is null)
        {
            return null;
        }

        // 解析类型
        var type = Type.GetType(dbEntity.ContextType, throwOnError: true);
        if (type is null || !typeof(IStateMachineContext).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Invalid context type: {dbEntity.ContextType}");
        }

        var context = (IStateMachineContext?)JsonSerializer.Deserialize(dbEntity.ContextData, type, DefaultJsonSerializerOptions.Options);
        return context is null
            ? throw new InvalidOperationException($"Failed to deserialize context of type {type.FullName}.")
            : (type, context);
    }

    public void Add(IStateMachineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new InvalidOperationException("Context must have a valid CorrelationId.");
        }

        var type = context.GetType();
        var contextData = JsonSerializer.Serialize(context, type, DefaultJsonSerializerOptions.Options);

        var dbEntity = new StateMachineContextInfo
        {
            CorrelationId = correlationId,
            ContextType = type.AssemblyQualifiedName!,
            ContextData = contextData
        };

        _dbContext.Set<StateMachineContextInfo>().Add(dbEntity);
    }

    public void Update(IStateMachineContext context)
    {
        var correlationId = context.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new InvalidOperationException("Context must have a valid CorrelationId.");
        }

        var dbEntity = _dbContext.StateMachineContexts.Find(correlationId);

        if (dbEntity is null)
        {
            return;
        }

        var type = context.GetType();

        if (type.AssemblyQualifiedName != dbEntity.ContextType)
        {
            throw new InvalidOperationException($"Invalid context type: {type.AssemblyQualifiedName}");
        }


        var contextData = JsonSerializer.Serialize(context, type, DefaultJsonSerializerOptions.Options);
        if (contextData != dbEntity.ContextData)
        {
            dbEntity.ContextData = contextData;
        }
    }
}
