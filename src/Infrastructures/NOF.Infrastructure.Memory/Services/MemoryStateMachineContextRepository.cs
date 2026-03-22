using NOF.Application;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryStateMachineContextRepository : MemoryRepository<NOFStateMachineContext, string, string>, IStateMachineContextRepository
{
    public MemoryStateMachineContextRepository(MemoryPersistenceContext context)
        : base(
            context,
            static context => (context.CorrelationId, context.DefinitionTypeName))
    {
    }
}
