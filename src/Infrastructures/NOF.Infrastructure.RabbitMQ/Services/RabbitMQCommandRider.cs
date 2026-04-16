using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQCommandRider : ICommandRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly IObjectSerializer _serializer;

    public RabbitMQCommandRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        IObjectSerializer serializer)
    {
        _connectionManager = connectionManager;
        _options = options;
        _serializer = serializer;
    }

    public async Task SendAsync(object command,
        Type commandType,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandType);
        await PublishToRabbitMQAsync(command, commandType, headers, cancellationToken);
    }

    private async Task PublishToRabbitMQAsync(object command, Type commandType, IEnumerable<KeyValuePair<string, string?>>? headers, CancellationToken cancellationToken)
    {
        await using var channel = await _connectionManager.CreateChannelAsync();

        var exchangeName = commandType.FullName ?? commandType.Name;
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
            Type = command.GetType().FullName
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

        var body = _serializer.Serialize(command);

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body,
            mandatory: false,
            cancellationToken: cancellationToken);
    }
}
