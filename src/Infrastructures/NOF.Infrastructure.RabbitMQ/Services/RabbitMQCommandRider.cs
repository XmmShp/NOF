using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQCommandRider : ICommandRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly IMessageSerializer _serializer;

    public RabbitMQCommandRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        IMessageSerializer serializer)
    {
        _connectionManager = connectionManager;
        _options = options;
        _serializer = serializer;
    }

    public async Task SendAsync(ICommand command,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        await PublishToRabbitMQAsync(command, executionContext, cancellationToken);
    }

    private async Task PublishToRabbitMQAsync(ICommand command, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        await using var channel = await _connectionManager.CreateChannelAsync();

        var commandType = command.GetType();
        var exchangeName = $"nof.command.{commandType.Name}";
        var queueName = $"nof.command.{commandType.Name}";
        var routingKey = commandType.Name;

        // 声明 direct exchange（用于 command）
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: "direct",
            durable: _options.Value.Durable,
            autoDelete: _options.Value.AutoDelete,
            cancellationToken: cancellationToken);

        // 声明 queue（确保发送时 queue 存在，消息可以被保留）
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: _options.Value.Durable,
            exclusive: false,
            autoDelete: _options.Value.AutoDelete,
            cancellationToken: cancellationToken);

        // 绑定 queue 到 exchange
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = _options.Value.Durable,
            ContentType = "application/json",
            Type = command.GetType().FullName
        };

        properties.Headers = executionContext.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);

        var messageString = _serializer.Serialize(command);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageString);
        var body = new ReadOnlyMemory<byte>(messageBytes);
        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body,
            mandatory: false,
            cancellationToken: cancellationToken);
    }
}
