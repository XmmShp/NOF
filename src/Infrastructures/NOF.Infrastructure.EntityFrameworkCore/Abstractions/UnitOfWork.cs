using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NOF;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly IEventPublisher _publisher;
    private readonly ITransactionalMessageRepository _messageRepository;
    private readonly ICommandSender _commandSender;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ITransactionalMessageCollector _collector;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(
        DbContext dbContext,
        IEventPublisher publisher,
        ITransactionalMessageRepository messageRepository,
        ICommandSender commandSender,
        INotificationPublisher notificationPublisher,
        ITransactionalMessageCollector collector,
        ILogger<UnitOfWork> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _messageRepository = messageRepository;
        _commandSender = commandSender;
        _notificationPublisher = notificationPublisher;
        _collector = collector;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var domainEvents = _dbContext.ChangeTracker.Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .SelectMany(e => { var events = e.Events.ToList(); e.ClearEvents(); return events; }).ToList();

        var messages = _collector.GetMessages();

        var messageIds = messages.Select(m => m.Id).ToList();

        if (messages.Count > 0)
        {
            _messageRepository.Add(messages, cancellationToken);
        }

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        _collector.Clear();

        if (messageIds.Count > 0)
        {
            await TrySendMessagesImmediatelyAsync(messageIds, cancellationToken);
        }

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await _publisher.PublishAsync(domainEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "An exception has occured when publishing event: {Message}", ex.Message);
                }

                throw;
            }
        }

        return result;
    }

    private async Task TrySendMessagesImmediatelyAsync(
        List<Guid> messageIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = await _dbContext.Set<TransactionalMessage>()
                .Where(m => messageIds.Contains(m.Id))
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    if (message.MessageType == OutboxMessageType.Command)
                    {
                        var command = Deserialize<ICommand>(message.PayloadType, message.Payload);
                        await _commandSender.SendAsync(command, message.DestinationEndpointName, cancellationToken);

                        _logger.LogDebug("Immediately sent command {MessageId} of type {MessageType}",
                            message.Id, command.GetType().Name);
                    }
                    else if (message.MessageType == OutboxMessageType.Notification)
                    {
                        var notification = Deserialize<INotification>(message.PayloadType, message.Payload);
                        await _notificationPublisher.PublishAsync(notification, cancellationToken);

                        _logger.LogDebug("Immediately published notification {MessageId} of type {MessageType}",
                            message.Id, notification.GetType().Name);
                    }

                    message.Status = OutboxMessageStatus.Sent;
                    message.SentAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to immediately send message {MessageId}", message.Id);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in immediate message sending");
        }
    }

    private T Deserialize<T>(string typeName, string payload)
    {
        var type = Type.GetType(typeName)
            ?? throw new InvalidOperationException($"Cannot resolve type: {typeName}");

        return (T)System.Text.Json.JsonSerializer.Deserialize(payload, type)!;
    }
}
