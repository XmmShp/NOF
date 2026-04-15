using Microsoft.EntityFrameworkCore;
using NOF.Application;

namespace NOF.Infrastructure;

internal sealed class EFCoreStateMachineContextStore : IStateMachineContextStore
{
    private readonly DbContext _dbContext;

    public EFCoreStateMachineContextStore(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<NOFStateMachineContext?> FindAsync(
        string correlationId,
        string definitionTypeName,
        CancellationToken cancellationToken = default)
    {
        // Key shape is defined by NOFDbContext OnModelCreating; NOFDbContext will append tenant key if needed.
        return await _dbContext.FindAsync<NOFStateMachineContext>(
            keyValues: [correlationId, definitionTypeName],
            cancellationToken: cancellationToken);
    }

    public void Add(NOFStateMachineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _dbContext.Set<NOFStateMachineContext>().Add(context);
    }
}
