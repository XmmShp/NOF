using Microsoft.Extensions.Logging;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>Inbox message processing step — deduplication via inbox pattern.</summary>
public class MessageInboxMiddlewareStep : IHandlerMiddlewareStep<MessageInboxMiddleware>, IAfter<AutoInstrumentationMiddlewareStep>;

/// <summary>
/// Inbox middleware
/// Responsible for recording inbox messages in transactions to ensure reliable message processing
/// </summary>
public sealed class MessageInboxMiddleware : IHandlerMiddleware
{
    private readonly ITransactionManager _transactionManager;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MessageInboxMiddleware> _logger;
    private readonly IInvocationContext _invocationContext;

    public MessageInboxMiddleware(ITransactionManager transactionManager, IInboxMessageRepository inboxMessageRepository, IUnitOfWork unitOfWork, ILogger<MessageInboxMiddleware> logger, IInvocationContext invocationContext)
    {
        _transactionManager = transactionManager;
        _inboxMessageRepository = inboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _invocationContext = invocationContext;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        await using var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);

        try
        {
            context.Headers.TryGetValue(NOFConstants.Headers.MessageId, out var messageIdStr);
            var messageId = Guid.TryParse(messageIdStr, out var parsed) ? parsed : Guid.NewGuid();

            context.Headers[NOFConstants.Headers.MessageId] = messageId.ToString();

            var messageExists = await _inboxMessageRepository.ExistByMessageIdAsync(messageId, cancellationToken);
            if (messageExists)
            {
                _logger.LogDebug("Inbox message {MessageId} for {MessageType} already exists, skipping processing", messageId, context.MessageType);

                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var inboxMessage = new InboxMessage(messageId);

            _inboxMessageRepository.Add(inboxMessage);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await next(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug(
                "Inbox message {MessageId} for {MessageType} processed and committed successfully",
                messageId, context.MessageType);
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx,
                    "Failed to rollback transaction for inbox message processing of {MessageType}",
                    context.MessageType);
            }

            _logger.LogError(ex,
                "Failed to process inbox message for {MessageType}. Transaction has been rolled back.",
                context.MessageType);

            throw;
        }
    }
}
