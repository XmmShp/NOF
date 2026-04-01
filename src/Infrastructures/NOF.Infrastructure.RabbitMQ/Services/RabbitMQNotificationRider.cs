using Microsoft.Extensions.Options;
using NOF.Contract;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQNotificationRider : INotificationRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly IMessageSerializer _serializer;

    public RabbitMQNotificationRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        IMessageSerializer serializer)
    {
        _connectionManager = connectionManager;
        _options = options;
        _serializer = serializer;
    }

    public async Task PublishAsync(INotification notification,
        IDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await using var channel = await _connectionManager.CreateChannelAsync();

        var notificationType = notification.GetType();
        var exchangeName = $"nof.notification.{notificationType.Name}";

        // 声明 fanout exchange（用于 notification）
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: "fanout",
            durable: _options.Value.Durable,
            autoDelete: _options.Value.AutoDelete,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = _options.Value.Durable,
            ContentType = "application/json",
            Type = notification.GetType().FullName
        };

        if (headers != null)
        {
            properties.Headers = headers.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        }

        var messageString = _serializer.Serialize(notification);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageString);
        var body = new ReadOnlyMemory<byte>(messageBytes);
        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty, // fanout exchange 不需要 routing key
            basicProperties: properties,
            body: body,
            mandatory: false,
            cancellationToken: cancellationToken);
    }
}
