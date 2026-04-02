using Microsoft.Extensions.Options;
using NOF.Application;
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
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        await using var channel = await _connectionManager.CreateChannelAsync();

        var notificationType = notification.GetType();
        var exchangeName = notificationType.FullName ?? notificationType.Name;

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

        properties.Headers = executionContext.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);

        var messageString = _serializer.Serialize(notification);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageString);
        var body = new ReadOnlyMemory<byte>(messageBytes);

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            basicProperties: properties,
            body: body,
            mandatory: false,
            cancellationToken: cancellationToken);
    }
}
