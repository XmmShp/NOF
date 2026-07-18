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
        string messageRoute,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageRoute);

        await using var channel = await _connectionManager.CreateChannelAsync();

        var properties = new BasicProperties
        {
            Persistent = _options.Value.Durable,
            ContentType = "application/octet-stream"
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

        await channel.ExchangeDeclareAsync(
            exchange: messageRoute,
            type: "fanout",
            durable: _options.Value.Durable,
            autoDelete: _options.Value.AutoDelete,
            cancellationToken: cancellationToken);

        await channel.BasicPublishAsync(
            exchange: messageRoute,
            routingKey: string.Empty,
            basicProperties: properties,
            body: payload,
            mandatory: false,
            cancellationToken: cancellationToken);
    }
}
