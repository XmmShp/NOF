using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>Inbox message processing step deduplication via inbox pattern.</summary>
/// <summary>
/// Inbox middleware
/// Responsible for recording inbox messages in transactions to ensure reliable message processing
/// </summary>
public sealed class MessageInboxInboundMiddleware : IInboundMiddleware, IAfter<AutoInstrumentationInboundMiddleware>
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

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        _executionContext.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var messageIdStr);
        if (!Guid.TryParse(messageIdStr, out var messageId))
        {
            // No message id => no inbox dedup. Don't create a transaction or write inbox entry.
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
                var messageName = context.Metadatas.TryGetValue("MessageName", out var mn) ? mn as string : context.Message?.GetType().FullName ?? "<null>";
                _logger.LogDebug("Inbox message {MessageId} for {MessageType} already exists, skipping processing", messageId, messageName);

                // Short-circuit with an explicit failure result so upstream callers can handle gracefully.
                context.Response = Result.Fail("409", "Duplicate message detected by inbox deduplication.");
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var inboxMessage = new NOFInboxMessage(messageId);

            _dbContext.Set<NOFInboxMessage>().Add(inboxMessage);

            await _dbContext.SaveChangesAsync(cancellationToken);
            _executionContext.Remove(NOFAbstractionConstants.Transport.Headers.MessageId);

            await next(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var messageName2 = context.Metadatas.TryGetValue("MessageName", out var mn2) ? mn2 as string : context.Message?.GetType().FullName ?? "<null>";
            _logger.LogDebug(
                "Inbox message {MessageId} for {MessageType} processed and committed successfully",
                messageId, messageName2);
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                var messageName3 = context.Metadatas.TryGetValue("MessageName", out var mn3) ? mn3 as string : context.Message?.GetType().FullName ?? "<null>";
                _logger.LogError(rollbackEx,
                    "Failed to rollback transaction for inbox message processing of {MessageType}",
                    messageName3);
            }

            var messageName4 = context.Metadatas.TryGetValue("MessageName", out var mn4) ? mn4 as string : context.Message?.GetType().FullName ?? "<null>";
            _logger.LogError(ex,
                "Failed to process inbox message for {MessageType}. Transaction has been rolled back.",
                messageName4);

            throw;
        }
    }
}
