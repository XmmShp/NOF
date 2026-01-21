using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// 收件箱消息仓储实现
/// </summary>
internal sealed class InboxMessageRepository : IInboxMessageRepository
{
    private readonly NOFDbContext _dbContext;
    private readonly ILogger<InboxMessageRepository> _logger;

    public InboxMessageRepository(NOFDbContext dbContext, ILogger<InboxMessageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public void Add(InboxMessage message)
    {
        var entity = new EFCoreInboxMessage
        {
            Id = message.Id,
            MessageType = message.MessageType,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            ProcessedAt = message.ProcessedAt,
            Status = (EFCoreInboxMessageStatus)message.Status,
            RetryCount = message.RetryCount,
            ErrorMessage = message.ErrorMessage
        };

        _dbContext.InboxMessages.Add(entity);

        _logger.LogDebug("Added inbox message {MessageId} of type {MessageType}",
            message.Id, message.MessageType);
    }

    public async Task<bool> ExistByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.InboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == messageId, cancellationToken);

        _logger.LogDebug("Checked existence of inbox message {MessageId}: {Exists}",
            messageId, exists);

        return exists;
    }
}
