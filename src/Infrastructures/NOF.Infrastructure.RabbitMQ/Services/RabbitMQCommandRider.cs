using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQCommandRider : ICommandRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;

    public RabbitMQCommandRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options)
    {
        _connectionManager = connectionManager;
        _options = options;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload,
        string payloadTypeName,
        string commandTypeName,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        await PublishToRabbitMQAsync(payload, payloadTypeName, commandTypeName, headers, cancellationToken);
    }

    private async Task PublishToRabbitMQAsync(ReadOnlyMemory<byte> payload, string payloadTypeName, string commandTypeName, IEnumerable<KeyValuePair<string, string?>>? headers, CancellationToken cancellationToken)
    {
        await using var channel = await _connectionManager.CreateChannelAsync();

        var exchangeName = commandTypeName;
        var routingKey = exchangeName;

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: "direct",
            durable: _options.Value.Durable,
            autoDelete: _options.Value.AutoDelete,
            cancellationToken: cancellationToken);

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

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: payload,
            mandatory: false,
            cancellationToken: cancellationToken);
    }
}
