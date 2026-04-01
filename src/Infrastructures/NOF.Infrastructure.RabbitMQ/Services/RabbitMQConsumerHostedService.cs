using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQConsumerHostedService : IHostedService, IDisposable
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageSerializer _serializer;
    private readonly HandlerInfos? _handlerInfos;
    private readonly ILogger<RabbitMQConsumerHostedService> _logger;
    private readonly List<IChannel> _channels = new();
    private readonly Dictionary<string, Type> _consumerTypes = new();
    private bool _disposed;

    public RabbitMQConsumerHostedService(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        IServiceProvider serviceProvider,
        IMessageSerializer serializer,
        HandlerInfos? handlerInfos,
        ILogger<RabbitMQConsumerHostedService> logger)
    {
        _connectionManager = connectionManager;
        _options = options;
        _serviceProvider = serviceProvider;
        _serializer = serializer;
        _handlerInfos = handlerInfos;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 从 HandlerInfos 注册所有消费者
            await RegisterConsumersFromHandlerInfosAsync();
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

    // 从 HandlerInfos 注册所有消费者
    private async Task RegisterConsumersFromHandlerInfosAsync()
    {
        if (_handlerInfos == null)
        {
            return;
        }

        // 注册命令消费者
        foreach (var commandInfo in _handlerInfos.Commands)
        {
            var queueName = $"nof.command.{commandInfo.CommandType.Name}";
            _consumerTypes[queueName] = commandInfo.HandlerType;
            await SetupConsumerAsync(queueName, commandInfo.CommandType);
        }

        // 注册通知消费者
        foreach (var notificationInfo in _handlerInfos.Notifications)
        {
            var queueName = $"nof.notification.{notificationInfo.NotificationType.Name}";
            _consumerTypes[queueName] = notificationInfo.HandlerType;
            await SetupConsumerAsync(queueName, notificationInfo.NotificationType);
        }
    }

    private async Task SetupConsumerAsync(string queueName, Type messageType)
    {
        var channel = await _connectionManager.CreateChannelAsync();
        _channels.Add(channel);

        string exchangeName;
        string exchangeType;
        string routingKey;

        // 根据消息类型设置不同的拓扑结构
        if (typeof(ICommand).IsAssignableFrom(messageType))
        {
            // Command：使用 direct exchange，每个 Command 类型一个 exchange
            exchangeName = $"nof.command.{messageType.Name}";
            exchangeType = "direct";
            routingKey = messageType.Name;
        }
        else if (typeof(INotification).IsAssignableFrom(messageType))
        {
            // Notification：使用 fanout exchange，每个 Notification 类型一个 exchange
            exchangeName = $"nof.notification.{messageType.Name}";
            exchangeType = "fanout";
            routingKey = string.Empty; // fanout exchange 不需要 routing key
        }
        else
        {
            throw new NotSupportedException($"Unsupported message type: {messageType.Name}");
        }

        // 声明 exchange
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: exchangeType,
            durable: _options.Value.Durable,
            autoDelete: _options.Value.AutoDelete);

        // 声明 queue（多实例会共享同一个 queue，由 RabbitMQ 做负载均衡）
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: _options.Value.Durable,
            exclusive: false,
            autoDelete: _options.Value.AutoDelete);

        // 绑定 queue 到 exchange
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        await channel.BasicQosAsync(0, _options.Value.PrefetchCount, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, args) => await HandleMessageAsync(queueName, (AsyncEventingBasicConsumer)sender, args);

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer);
    }

    private async Task HandleMessageAsync(string queueName, AsyncEventingBasicConsumer consumer, BasicDeliverEventArgs args)
    {
        try
        {
            if (!_consumerTypes.TryGetValue(queueName, out var handlerType))
            {
                await consumer.Channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            var messageType = Type.GetType(args.BasicProperties.Type!);
            if (messageType == null)
            {
                await consumer.Channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            var messageBytes = args.Body.ToArray();
            var payload = System.Text.Encoding.UTF8.GetString(messageBytes);
            var message = _serializer.Deserialize(messageType.FullName!, payload);
            if (message == null)
            {
                await consumer.Channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetService(handlerType);
            if (handler == null)
            {
                await consumer.Channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            if (handler is ICommandHandler commandHandler && message is ICommand command)
            {
                await commandHandler.HandleAsync(command, CancellationToken.None);
            }
            else if (handler is INotificationHandler notificationHandler && message is INotification notification)
            {
                await notificationHandler.HandleAsync(notification, CancellationToken.None);
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
