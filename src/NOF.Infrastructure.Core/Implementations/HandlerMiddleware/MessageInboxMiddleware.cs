using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// 收件箱中间件
/// 负责在事务中记录收件箱消息，确保消息的可靠处理
/// </summary>
public sealed class MessageInboxMiddleware : IHandlerMiddleware
{
    private readonly ITransactionManager _transactionManager;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MessageInboxMiddleware> _logger;

    public MessageInboxMiddleware(
        ITransactionManager transactionManager,
        IInboxMessageRepository inboxMessageRepository,
        IUnitOfWork unitOfWork,
        ILogger<MessageInboxMiddleware> logger)
    {
        _transactionManager = transactionManager;
        _inboxMessageRepository = inboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        await using var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);

        try
        {
            var messageId = context.MessageId;
            var messageExists = await _inboxMessageRepository.ExistByMessageIdAsync(messageId, cancellationToken);
            if (messageExists)
            {
                _logger.LogDebug(
                    "Inbox message {MessageId} for {MessageType} already exists, skipping processing",
                    messageId, context.MessageType);

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