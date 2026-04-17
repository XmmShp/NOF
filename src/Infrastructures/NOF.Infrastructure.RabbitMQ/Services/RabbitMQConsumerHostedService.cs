using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQConsumerHostedService : IHostedService, IDisposable
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly CommandHandlerInfos? _commandHandlerInfos;
    private readonly NotificationHandlerInfos? _notificationHandlerInfos;
    private readonly InboxMessageStore _inboxMessageStore;
    private readonly ILogger<RabbitMQConsumerHostedService> _logger;
    private readonly List<IChannel> _channels = [];
    private readonly Dictionary<string, Type> _notificationHandlerTypes = new(StringComparer.Ordinal);
    private bool _disposed;

    public RabbitMQConsumerHostedService(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        CommandHandlerInfos? commandHandlerInfos,
        NotificationHandlerInfos? notificationHandlerInfos,
        InboxMessageStore inboxMessageStore,
        ILogger<RabbitMQConsumerHostedService> logger)
    {
        _connectionManager = connectionManager;
        _options = options;
        _commandHandlerInfos = commandHandlerInfos;
        _notificationHandlerInfos = notificationHandlerInfos;
        _inboxMessageStore = inboxMessageStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RegisterConsumersFromHandlerInfosAsync(cancellationToken);
            _logger.LogInformation("RabbitMQ consumers initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ consumers");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    private async Task RegisterConsumersFromHandlerInfosAsync(CancellationToken cancellationToken)
    {
        if (_commandHandlerInfos is null && _notificationHandlerInfos is null)
        {
            return;
        }

        var commandTypes = (_commandHandlerInfos?.Registrations ?? [])
            .Select(info => info.CommandType)
            .Distinct()
            .ToArray();

        await SetupCommandConsumersAsync(commandTypes, cancellationToken);

        var notificationGroups = (_notificationHandlerInfos?.Registrations ?? [])
            .GroupBy(info => info.HandlerType)
            .ToArray();

        foreach (var group in notificationGroups)
        {
            var handlerType = group.Key;
            var queueName = handlerType.DisplayName;
            var notificationTypes = group
                .Select(info => info.NotificationType)
                .Distinct()
                .ToArray();

            _notificationHandlerTypes[queueName] = handlerType;
            await SetupNotificationConsumerAsync(queueName, notificationTypes, cancellationToken);
        }
    }

    private async Task SetupCommandConsumersAsync(Type[] commandTypes, CancellationToken cancellationToken)
    {
        if (commandTypes.Length == 0)
        {
            return;
        }

        foreach (var commandType in commandTypes)
        {
            var channel = await _connectionManager.CreateChannelAsync();
            _channels.Add(channel);

            var exchangeName = GetMessageExchangeName(commandType);
            var queueName = exchangeName;

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: "direct",
                durable: _options.Value.Durable,
                autoDelete: _options.Value.AutoDelete,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: _options.Value.Durable,
                exclusive: false,
                autoDelete: _options.Value.AutoDelete,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: exchangeName,
                cancellationToken: cancellationToken);

            await channel.BasicQosAsync(0, _options.Value.PrefetchCount, false, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, args) => await HandleMessageAsync(queueName, (AsyncEventingBasicConsumer)sender, args);

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }
    }

    private async Task SetupNotificationConsumerAsync(string queueName, IReadOnlyCollection<Type> notificationTypes, CancellationToken cancellationToken)
    {
        if (notificationTypes.Count == 0)
        {
            return;
        }

        var channel = await _connectionManager.CreateChannelAsync();
        _channels.Add(channel);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: _options.Value.Durable,
            exclusive: false,
            autoDelete: _options.Value.AutoDelete,
            cancellationToken: cancellationToken);

        foreach (var notificationType in notificationTypes)
        {
            var exchangeName = GetMessageExchangeName(notificationType);
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: "fanout",
                durable: _options.Value.Durable,
                autoDelete: _options.Value.AutoDelete,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: string.Empty,
                cancellationToken: cancellationToken);
        }

        await channel.BasicQosAsync(0, _options.Value.PrefetchCount, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, args) => await HandleMessageAsync(queueName, (AsyncEventingBasicConsumer)sender, args);

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    private async Task HandleMessageAsync(string queueName, AsyncEventingBasicConsumer consumer, BasicDeliverEventArgs args)
    {
        try
        {
            var payload = args.Body;

            var messageTypeName = args.BasicProperties.Type;
            if (string.IsNullOrWhiteSpace(messageTypeName))
            {
                throw new InvalidOperationException("RabbitMQ message type was missing in BasicProperties.Type.");
            }

            Dictionary<string, string?>? headers = null;
            if (args.BasicProperties.Headers is not null)
            {
                headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in args.BasicProperties.Headers)
                {
                    if (value is byte[] bytes)
                    {
                        headers[key] = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                }
            }

            if (_notificationHandlerTypes.TryGetValue(queueName, out var notificationHandlerType))
            {
                var messageId = ResolveMessageId(headers);
                await _inboxMessageStore.EnqueueAsync(
                    messageId,
                    InboxMessageType.Notification,
                    payload,
                    messageTypeName,
                    TypeRegistry.Register(notificationHandlerType),
                    headers,
                    CancellationToken.None);
            }
            else
            {
                if (_commandHandlerInfos is null)
                {
                    throw new InvalidOperationException("Command handler infos are not available.");
                }

                var commandType = TypeRegistry.Resolve(queueName);
                var handlerType = _commandHandlerInfos.GetHandlers(commandType).FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Cannot route command '{commandType.Name}'. No matching handler registered.");

                var messageId = ResolveMessageId(headers);
                await _inboxMessageStore.EnqueueAsync(
                    messageId,
                    InboxMessageType.Command,
                    payload,
                    messageTypeName,
                    TypeRegistry.Register(handlerType),
                    headers,
                    CancellationToken.None);
            }

            await consumer.Channel.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from queue {QueueName}", queueName);
            try
            {
                await consumer.Channel.BasicNackAsync(args.DeliveryTag, false, false);
            }
            catch
            {
                // Ignore
            }
        }
    }

    private static string GetMessageExchangeName(Type messageType)
        => messageType.DisplayName;

    private static Guid ResolveMessageId(Dictionary<string, string?>? headers)
    {
        if (headers is not null
            && headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.MessageId, out var value)
            && Guid.TryParse(value, out var messageId))
        {
            return messageId;
        }

        return Guid.NewGuid();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var channel in _channels)
        {
            try
            {
                channel.Dispose();
            }
            catch
            {
                // Ignore
            }
        }

        _disposed = true;
    }
}
