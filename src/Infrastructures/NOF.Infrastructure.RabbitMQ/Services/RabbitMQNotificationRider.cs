using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQNotificationRider : INotificationRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;

    public RabbitMQNotificationRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options)
    {
        _connectionManager = connectionManager;
        _options = options;
    }

    public async Task PublishAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        IReadOnlyCollection<string> notificationTypeNames,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        await using var channel = await _connectionManager.CreateChannelAsync();

        var properties = new BasicProperties
        {
            Persistent = _options.Value.Durable,
            ContentType = "application/octet-stream",
            Type = payloadTypeName
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

        foreach (var notificationTypeName in notificationTypeNames)
        {
            var exchangeName = notificationTypeName;

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
                body: payload,
                mandatory: false,
                cancellationToken: cancellationToken);
        }
    }
}
