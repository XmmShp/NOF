using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class MessageInboxInboundMiddleware : CommandAndNotificationInboundMiddleware, IAfter<AutoInstrumentationInboundMiddleware>
{
    private readonly DbContext _dbContext;
    private readonly ILogger<MessageInboxInboundMiddleware> _logger;
    private readonly IExecutionContext _executionContext;

    public MessageInboxInboundMiddleware(DbContext dbContext, ILogger<MessageInboxInboundMiddleware> logger, IExecutionContext executionContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _executionContext = executionContext;
    }

    protected override async ValueTask InvokeAsyncCore(MessageInboundContext context, Func<CancellationToken, ValueTask> next, CancellationToken cancellationToken)
    {
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var messageIdStr);
        if (!Guid.TryParse(messageIdStr, out var messageId))
        {
            await next(cancellationToken);
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var messageExists = await _dbContext.FindAsync<NOFInboxMessage>(
                keyValues: [messageId],
                cancellationToken: cancellationToken) is not null;
            if (messageExists)
            {
                var messageName = context.MessageName ?? "<null>";
                _logger.LogDebug("Inbox message {MessageId} for {MessageType} already exists, skipping processing", messageId, messageName);
                context.Response = Result.Fail("409", "Duplicate message detected by inbox deduplication.");
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            _dbContext.Set<NOFInboxMessage>().Add(new NOFInboxMessage(messageId));
            await _dbContext.SaveChangesAsync(cancellationToken);
            _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.MessageId);

            await next(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug("Inbox message {MessageId} for {MessageType} processed and committed successfully", messageId, context.MessageName ?? "<null>");
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to rollback transaction for inbox message processing of {MessageType}", context.MessageName ?? "<null>");
            }

            _logger.LogError(ex, "Failed to process inbox message for {MessageType}. Transaction has been rolled back.", context.MessageName ?? "<null>");
            throw;
        }
    }
}
