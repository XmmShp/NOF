using NOF.Application;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class EFCoreStateMachineContextRepository : EFCoreRepository<NOFStateMachineContext>, IStateMachineContextRepository
{
    public EFCoreStateMachineContextRepository(NOFDbContext dbContext) : base(dbContext)
    {
    }
}
