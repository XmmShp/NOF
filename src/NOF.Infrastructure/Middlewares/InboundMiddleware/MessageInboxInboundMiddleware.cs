using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>Inbox message processing step deduplication via inbox pattern.</summary>
public class MessageInboxInboundMiddlewareStep : IInboundMiddlewareStep<MessageInboxInboundMiddlewareStep, MessageInboxInboundMiddleware>, IAfter<AutoInstrumentationInboundMiddlewareStep>;

/// <summary>
/// Inbox middleware
/// Responsible for recording inbox messages in transactions to ensure reliable message processing
/// </summary>
public sealed class MessageInboxInboundMiddleware : IInboundMiddleware
{
    private readonly ITransactionManager _transactionManager;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MessageInboxInboundMiddleware> _logger;
    private readonly IExecutionContext _executionContext;

    public MessageInboxInboundMiddleware(ITransactionManager transactionManager, IInboxMessageRepository inboxMessageRepository, IUnitOfWork unitOfWork, ILogger<MessageInboxInboundMiddleware> logger, IExecutionContext executionContext)
    {
        _transactionManager = transactionManager;
        _inboxMessageRepository = inboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        await using var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);

        try
        {
            _executionContext.TryGetValue(NOFContractConstants.Transport.Headers.MessageId, out var messageIdStr);
            var messageId = Guid.TryParse(messageIdStr, out var parsed) ? parsed : Guid.NewGuid();
            var messageExists = await _inboxMessageRepository.ExistsAsync(messageId, cancellationToken);
            if (messageExists)
            {
                _logger.LogDebug("Inbox message {MessageId} for {MessageType} already exists, skipping processing", messageId, context.Message.GetType().FullName);

                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var inboxMessage = new NOFInboxMessage(messageId);

            _inboxMessageRepository.Add(inboxMessage);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _executionContext.Remove(NOFContractConstants.Transport.Headers.MessageId);

            await next(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug(
                "Inbox message {MessageId} for {MessageType} processed and committed successfully",
                messageId, context.Message.GetType().FullName);
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
                    context.Message.GetType().FullName);
            }

            _logger.LogError(ex,
                "Failed to process inbox message for {MessageType}. Transaction has been rolled back.",
                context.Message.GetType().FullName);

            throw;
        }
    }
}
