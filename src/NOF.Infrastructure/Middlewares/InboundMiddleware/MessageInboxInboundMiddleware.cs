using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

public sealed class CommandMessageInboxInboundMiddleware : ICommandInboundMiddleware, IAfter<CommandAutoInstrumentationInboundMiddleware>
{
    private readonly DbContext _dbContext;
    private readonly ILogger<CommandMessageInboxInboundMiddleware> _logger;
    private readonly IExecutionContext _executionContext;

    public CommandMessageInboxInboundMiddleware(DbContext dbContext, ILogger<CommandMessageInboxInboundMiddleware> logger, IExecutionContext executionContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
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
                var messageName = context.MessageType.FullName ?? context.MessageType.Name;
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

            _logger.LogDebug("Inbox message {MessageId} for {MessageType} processed and committed successfully", messageId, context.MessageType.FullName ?? context.MessageType.Name);
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to rollback transaction for inbox message processing of {MessageType}", context.MessageType.FullName ?? context.MessageType.Name);
            }

            _logger.LogError(ex, "Failed to process inbox message for {MessageType}. Transaction has been rolled back.", context.MessageType.FullName ?? context.MessageType.Name);
            throw;
        }
    }
}

public sealed class NotificationMessageInboxInboundMiddleware : INotificationInboundMiddleware, IAfter<NotificationAutoInstrumentationInboundMiddleware>
{
    private readonly DbContext _dbContext;
    private readonly ILogger<NotificationMessageInboxInboundMiddleware> _logger;
    private readonly IExecutionContext _executionContext;

    public NotificationMessageInboxInboundMiddleware(DbContext dbContext, ILogger<NotificationMessageInboxInboundMiddleware> logger, IExecutionContext executionContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
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
                var messageName = context.MessageType.FullName ?? context.MessageType.Name;
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

            _logger.LogDebug("Inbox message {MessageId} for {MessageType} processed and committed successfully", messageId, context.MessageType.FullName ?? context.MessageType.Name);
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to rollback transaction for inbox message processing of {MessageType}", context.MessageType.FullName ?? context.MessageType.Name);
            }

            _logger.LogError(ex, "Failed to process inbox message for {MessageType}. Transaction has been rolled back.", context.MessageType.FullName ?? context.MessageType.Name);
            throw;
        }
    }
}
