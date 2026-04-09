using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Domain;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>Inbox message processing step deduplication via inbox pattern.</summary>
/// <summary>
/// Inbox middleware
/// Responsible for recording inbox messages in transactions to ensure reliable message processing
/// </summary>
public sealed class MessageInboxInboundMiddleware : IInboundMiddleware, IAfter<AutoInstrumentationInboundMiddleware>
{
    private readonly ITransactionManager _transactionManager;
    private readonly IRepository<NOFInboxMessage, Guid> _inboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MessageInboxInboundMiddleware> _logger;
    private readonly IExecutionContext _executionContext;

    public MessageInboxInboundMiddleware(ITransactionManager transactionManager, IRepository<NOFInboxMessage, Guid> inboxMessageRepository, IUnitOfWork unitOfWork, ILogger<MessageInboxInboundMiddleware> logger, IExecutionContext executionContext)
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
            _executionContext.TryGetValue(NOFHostingConstants.Transport.Headers.MessageId, out var messageIdStr);
            var messageId = Guid.TryParse(messageIdStr, out var parsed) ? parsed : Guid.NewGuid();
            var messageExists = await _inboxMessageRepository.FindAsync(messageId, cancellationToken) is not null;
            if (messageExists)
            {
                var messageName = context.Metadatas.TryGetValue("MessageName", out var mn) ? mn as string : context.Message?.GetType().FullName ?? "<null>";
                _logger.LogDebug("Inbox message {MessageId} for {MessageType} already exists, skipping processing", messageId, messageName);

                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var inboxMessage = new NOFInboxMessage(messageId);

            _inboxMessageRepository.Add(inboxMessage);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _executionContext.Remove(NOFHostingConstants.Transport.Headers.MessageId);

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
