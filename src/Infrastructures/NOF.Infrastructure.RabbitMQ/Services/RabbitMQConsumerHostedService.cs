using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQConsumerHostedService : IHostedService, IDisposable
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly IServiceProvider _rootServiceProvider;
    private readonly IObjectSerializer _serializer;
    private readonly HandlerInfos? _handlerInfos;
    private readonly ILogger<RabbitMQConsumerHostedService> _logger;
    private readonly List<IChannel> _channels = [];
    private readonly Dictionary<string, Type> _notificationHandlerTypes = new(StringComparer.Ordinal);
    private bool _disposed;

    public RabbitMQConsumerHostedService(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        IServiceProvider rootServiceProvider,
        IObjectSerializer serializer,
        HandlerInfos? handlerInfos,
        ILogger<RabbitMQConsumerHostedService> logger)
    {
        _connectionManager = connectionManager;
        _options = options;
        _rootServiceProvider = rootServiceProvider;
        _serializer = serializer;
        _handlerInfos = handlerInfos;
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
        if (_handlerInfos is null)
        {
            return;
        }

        var commandTypes = _handlerInfos.Commands
            .Select(info => info.CommandType)
            .Distinct()
            .ToArray();

        await SetupCommandConsumersAsync(commandTypes, cancellationToken);

        var notificationGroups = _handlerInfos.Notifications
            .GroupBy(info => info.HandlerType)
            .ToArray();

        foreach (var group in notificationGroups)
        {
            var handlerType = group.Key;
            var queueName = handlerType.FullName ?? handlerType.Name;
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

            var messageType = TypeRegistry.Resolve(messageTypeName);
            var message = _serializer.Deserialize(payload, messageType);

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
                await InboundHandlerInvoker.ExecuteNotificationToHandlerAsync(
                    _rootServiceProvider,
                    message!,
                    notificationHandlerType,
                    headers,
                    CancellationToken.None);
            }
            else
            {
                var commandType = messageType;
                var handlerType = _handlerInfos?.GetCommandHandlers(commandType).FirstOrDefault()
                    ?? throw new InvalidOperationException($"Cannot route command '{commandType.Name}'. No matching handler registered.");
                await InboundHandlerInvoker.ExecuteCommandAsync(
                    _rootServiceProvider,
                    handlerType,
                    message!,
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
        => messageType.FullName ?? messageType.Name;

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
