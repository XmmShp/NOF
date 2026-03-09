using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// EF Core inbox message repository implementation.
/// </summary>
internal sealed class EFCoreInboxMessageRepository : EFCoreRepository<NOFInboxMessage>, IInboxMessageRepository
{
    private readonly ILogger<EFCoreInboxMessageRepository> _logger;

    public EFCoreInboxMessageRepository(NOFDbContext dbContext, ILogger<EFCoreInboxMessageRepository> logger) : base(dbContext)
    {
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var exists = await DbContext.Set<NOFInboxMessage>()
            .AsNoTracking()
            .AnyAsync(m => m.Id == messageId, cancellationToken);

        _logger.LogDebug("Checked existence of inbox message {MessageId}: {Exists}", messageId, exists);

        return exists;
    }
}
