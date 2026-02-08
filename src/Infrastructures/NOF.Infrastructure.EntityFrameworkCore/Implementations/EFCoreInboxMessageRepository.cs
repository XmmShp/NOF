using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// EF Core inbox message repository implementation.
/// </summary>
internal sealed class EFCoreInboxMessageRepository : IInboxMessageRepository
{
    private readonly NOFDbContext _dbContext;
    private readonly ILogger<EFCoreInboxMessageRepository> _logger;

    public EFCoreInboxMessageRepository(NOFDbContext dbContext, ILogger<EFCoreInboxMessageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void Add(InboxMessage message)
    {
        var entity = new EFCoreInboxMessage
        {
            Id = message.Id,
            CreatedAt = message.CreatedAt
        };

        _dbContext.InboxMessages.Add(entity);
    }

    public async Task<bool> ExistByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.InboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == messageId, cancellationToken);

        _logger.LogDebug("Checked existence of inbox message {MessageId}: {Exists}", messageId, exists);

        return exists;
    }
}
