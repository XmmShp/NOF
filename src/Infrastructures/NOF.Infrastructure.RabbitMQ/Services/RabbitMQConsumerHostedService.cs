using Microsoft.Extensions.DependencyInjection;
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
    private readonly CommandHandlerRegistry _commandHandlerRegistry;
    private readonly NotificationHandlerRegistry _notificationHandlerRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQConsumerHostedService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IObjectSerializer _objectSerializer;
    private readonly List<IChannel> _channels = [];
    private readonly Dictionary<string, string> _commandHandlerTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _notificationHandlerRoutes = new(StringComparer.Ordinal);
    private bool _disposed;

    public RabbitMQConsumerHostedService(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        CommandHandlerRegistry commandHandlerRegistry,
        NotificationHandlerRegistry notificationHandlerRegistry,
        IHostEnvironment hostEnvironment,
        IServiceProvider serviceProvider,
        IObjectSerializer objectSerializer,
        ILogger<RabbitMQConsumerHostedService> logger)
    {
        _connectionManager = connectionManager;
        _options = options;
        _commandHandlerRegistry = commandHandlerRegistry;
        _notificationHandlerRegistry = notificationHandlerRegistry;
        _hostEnvironment = hostEnvironment;
        _serviceProvider = serviceProvider;
        _objectSerializer = objectSerializer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RegisterConsumersFromRegistryAsync(cancellationToken);
            _logger.LogInformation("RabbitMQ consumers initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ consumers");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    private async Task RegisterConsumersFromRegistryAsync(CancellationToken cancellationToken)
    {
        var commandRegistrations = _commandHandlerRegistry.Freeze()
            .Select(info => new
            {
                MessageRoute = info.CommandTypeName,
                info.HandlerTypeName,
                QueueName = BuildCommandQueueName(info.HandlerTypeName)
            })
            .Distinct()
            .ToArray();

        foreach (var registration in commandRegistrations)
        {
            _commandHandlerTypes[registration.QueueName] = registration.HandlerTypeName;
            await SetupCommandConsumerAsync(registration.QueueName, registration.MessageRoute, cancellationToken);
        }

        var notificationGroups = _notificationHandlerRegistry.Freeze()
            .GroupBy(info => info.HandlerType)
            .ToArray();

        foreach (var group in notificationGroups)
        {
            var handlerType = group.Key;
            var queueName = BuildNotificationQueueName(_hostEnvironment.ServiceName, handlerType.DisplayName);
            var notificationTypes = group
                .Select(info => info.NotificationType)
                .Distinct()
                .ToArray();

            _notificationHandlerRoutes[queueName] = handlerType.DisplayName;
            await SetupNotificationConsumerAsync(queueName, notificationTypes, cancellationToken);
        }
    }

    private async Task SetupCommandConsumerAsync(string queueName, string messageRoute, CancellationToken cancellationToken)
    {
        var channel = await _connectionManager.CreateChannelAsync();
        _channels.Add(channel);

        await channel.ExchangeDeclareAsync(
            exchange: messageRoute,
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
            exchange: messageRoute,
            routingKey: messageRoute,
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

            if (_notificationHandlerRoutes.TryGetValue(queueName, out var notificationHandlerTypeName))
            {
                var messageId = ResolveMessageId(headers);
                await EnqueueAsync(
                    messageId,
                    InboxMessageType.Notification,
                    payload,
                    queueName,
                    headers,
                    CancellationToken.None);
            }
            else if (_commandHandlerTypes.TryGetValue(queueName, out var commandHandlerTypeName))
            {
                var messageId = ResolveMessageId(headers);
                await EnqueueAsync(
                    messageId,
                    InboxMessageType.Command,
                    payload,
                    queueName,
                    headers,
                    CancellationToken.None);
            }
            else
            {
                throw new RabbitMQConsumerMessageException(
                    $"Cannot route queue '{queueName}'. No matching handler route registered.",
                    requeue: false);
            }

            await consumer.Channel.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            var requeue = ShouldRequeue(ex, _options.Value);
            _logger.LogError(
                ex,
                "Error handling message from queue {QueueName}. Requeue: {Requeue}",
                queueName,
                requeue);
            try
            {
                await consumer.Channel.BasicNackAsync(args.DeliveryTag, false, requeue);
            }
            catch
            {
                // Ignore
            }
        }
    }

    private async Task EnqueueAsync(
        Guid messageId,
        InboxMessageType messageType,
        ReadOnlyMemory<byte> payload,
        string route,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var dbContext = scope.ServiceProvider.GetService<IDbContext>();
        if (dbContext is null)
        {
            return;
        }

        dbContext.Set<NOFInboxMessage>().Add(new NOFInboxMessage
        {
            Id = messageId,
            MessageType = messageType,
            Route = route,
            Payload = payload.ToArray(),
            Headers = SerializeHeaders(headers)
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await InboxMessageExistsAsync(messageId, route, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(
                    "Inbox message {MessageId} for route {Route} already exists. Treating RabbitMQ delivery as duplicate.",
                    messageId,
                    route);
                return;
            }

            throw;
        }
    }

    private async Task<bool> InboxMessageExistsAsync(Guid messageId, string route, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            scope.ServiceProvider.ResolveDaemonServices();

            var dbContext = scope.ServiceProvider.GetService<IDbContext>();
            if (dbContext is null)
            {
                return false;
            }

            return await dbContext.Set<NOFInboxMessage>()
                .Where(message => message.Id == messageId && message.Route == route)
                .AnyAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private string SerializeHeaders(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var dictionary = headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        return _objectSerializer.SerializeToText(dictionary, typeof(Dictionary<string, string?>));
    }

    internal static bool ShouldRequeue(Exception exception, RabbitMQOptions options)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(options);

        return exception is RabbitMQConsumerMessageException messageException
            ? messageException.Requeue
            : options.RequeueOnConsumerFailure;
    }

    private static string GetMessageExchangeName(Type messageType)
        => messageType.DisplayName;

    internal static string BuildCommandQueueName(string handlerDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerDisplayName);
        return handlerDisplayName;
    }

    internal static string BuildNotificationQueueName(string? applicationName, string handlerDisplayName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            return handlerDisplayName;
        }

        return $"{applicationName}.{handlerDisplayName}";
    }

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
