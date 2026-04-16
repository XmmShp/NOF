using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQNotificationRider : INotificationRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly IObjectSerializer _serializer;

    public RabbitMQNotificationRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        IObjectSerializer serializer)
    {
        _connectionManager = connectionManager;
        _options = options;
        _serializer = serializer;
    }

    public async Task PublishAsync(object notification,
        Type[] notificationTypes,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationTypes);
        await using var channel = await _connectionManager.CreateChannelAsync();

        var properties = new BasicProperties
        {
            Persistent = _options.Value.Durable,
            ContentType = "application/octet-stream",
            Type = notification.GetType().FullName
        };

        if (headers is not null)
        {
            var headerDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in headers)
            {
                headerDict[k] = v;
            }
            properties.Headers = headerDict;
        }

        var body = _serializer.Serialize(notification);

        foreach (var notificationType in notificationTypes)
        {
            var exchangeName = notificationType.FullName ?? notificationType.Name;

            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: "fanout",
                durable: _options.Value.Durable,
                autoDelete: _options.Value.AutoDelete,
                cancellationToken: cancellationToken);

            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: string.Empty,
                basicProperties: properties,
                body: body,
                mandatory: false,
                cancellationToken: cancellationToken);
        }
    }
}
