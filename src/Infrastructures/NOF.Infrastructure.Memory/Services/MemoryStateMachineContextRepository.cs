using NOF.Application;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryStateMachineContextRepository : MemoryRepository<NOFStateMachineContext, string>, IStateMachineContextRepository
{
    public MemoryStateMachineContextRepository(MemoryPersistenceStore store, MemoryPersistenceSession session, IInvocationContext invocationContext)
        : base(
            store,
            session,
            () => $"nof:state-machine:{MemoryPersistenceStore.NormalizeTenantId(invocationContext.TenantId)}",
            static context => BuildKey(context.CorrelationId, context.DefinitionTypeName),
            static context => new NOFStateMachineContext
            {
                CorrelationId = context.CorrelationId,
                DefinitionTypeName = context.DefinitionTypeName,
                State = context.State
            },
            StringComparer.OrdinalIgnoreCase)
    {
    }

    public override ValueTask<NOFStateMachineContext?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default)
    {
        if (keyValues is not [string correlationId, string definitionTypeName])
        {
            return ValueTask.FromResult<NOFStateMachineContext?>(null);
        }

        return base.FindAsync([BuildKey(correlationId, definitionTypeName)], cancellationToken);
    }

    protected override IEnumerable<NOFStateMachineContext> OrderItems(IEnumerable<NOFStateMachineContext> items)
        => items.OrderBy(context => context.CorrelationId, StringComparer.OrdinalIgnoreCase);

    private static string BuildKey(string correlationId, string definitionTypeName)
        => $"{correlationId}\u001f{definitionTypeName}";
}
