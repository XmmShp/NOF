using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQCommandRider : ICommandRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly ILogger<RabbitMQCommandRider> _logger;

    public RabbitMQCommandRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options)
        : this(connectionManager, options, NullLogger<RabbitMQCommandRider>.Instance)
    {
    }

    public RabbitMQCommandRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        ILogger<RabbitMQCommandRider> logger)
    {
        _connectionManager = connectionManager;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload,
        string messageRoute,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        await PublishToRabbitMQAsync(payload, messageRoute, headers, cancellationToken);
    }

    private async Task PublishToRabbitMQAsync(ReadOnlyMemory<byte> payload, string messageRoute, IEnumerable<KeyValuePair<string, string?>>? headers, CancellationToken cancellationToken)
    {
        await using var channel = _options.Value.PublisherConfirmationsEnabled
            ? await _connectionManager.CreatePublisherChannelAsync()
            : await _connectionManager.CreateChannelAsync();

        var exchangeName = messageRoute;
        var queueName = RabbitMQTopology.BuildCommandQueueName(messageRoute);
        var routingKey = messageRoute;

        await RabbitMQTopology.DeclareSystemTopologyAsync(channel, cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: "direct",
            durable: _options.Value.Durable,
            autoDelete: _options.Value.AutoDelete,
            arguments: RabbitMQTopology.BuildBusinessExchangeArguments(),
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: _options.Value.Durable,
            exclusive: false,
            autoDelete: _options.Value.AutoDelete,
            arguments: RabbitMQTopology.BuildBusinessQueueArguments(),
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = _options.Value.Durable,
            ContentType = "application/octet-stream"
        };

        if (headers is not null)
        {
            properties.Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in headers)
            {
                properties.Headers[k] = v;
            }
        }

        properties.Headers ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        RabbitMQTopology.AddOriginalRouteHeaders(properties.Headers, exchangeName, routingKey);

        try
        {
            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: payload,
                mandatory: true,
                cancellationToken: cancellationToken);
        }
        catch (PublishException ex)
        {
            _logger.LogError(
                ex,
                "RabbitMQ rejected mandatory command publish. Route: {Route}, Exchange: {Exchange}, RoutingKey: {RoutingKey}, IsReturn: {IsReturn}, PublishSequenceNumber: {PublishSequenceNumber}",
                messageRoute,
                exchangeName,
                routingKey,
                ex.IsReturn,
                ex.PublishSequenceNumber);
            throw;
        }
    }
}
