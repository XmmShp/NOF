using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace NOF.Infrastructure.RabbitMQ;

public class RabbitMQNotificationRider : INotificationRider
{
    private readonly RabbitMQConnectionManager _connectionManager;
    private readonly IOptions<RabbitMQOptions> _options;
    private readonly ILogger<RabbitMQNotificationRider> _logger;

    public RabbitMQNotificationRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options)
        : this(connectionManager, options, NullLogger<RabbitMQNotificationRider>.Instance)
    {
    }

    public RabbitMQNotificationRider(
        RabbitMQConnectionManager connectionManager,
        IOptions<RabbitMQOptions> options,
        ILogger<RabbitMQNotificationRider> logger)
    {
        _connectionManager = connectionManager;
        _options = options;
        _logger = logger;
    }

    public async Task PublishAsync(ReadOnlyMemory<byte> payload,
        string messageRoute,
        IEnumerable<KeyValuePair<string, string?>>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageRoute);

        await using var channel = _options.Value.PublisherConfirmationsEnabled
            ? await _connectionManager.CreatePublisherChannelAsync()
            : await _connectionManager.CreateChannelAsync();

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
        RabbitMQTopology.AddOriginalRouteHeaders(properties.Headers, messageRoute, string.Empty);

        await RabbitMQTopology.DeclareSystemTopologyAsync(channel, cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: messageRoute,
            type: "fanout",
            durable: _options.Value.Durable,
            autoDelete: _options.Value.AutoDelete,
            arguments: RabbitMQTopology.BuildBusinessExchangeArguments(),
            cancellationToken: cancellationToken);

        try
        {
            await channel.BasicPublishAsync(
                exchange: messageRoute,
                routingKey: string.Empty,
                basicProperties: properties,
                body: payload,
                mandatory: true,
                cancellationToken: cancellationToken);
        }
        catch (PublishException ex)
        {
            _logger.LogError(
                ex,
                "RabbitMQ rejected mandatory notification publish. Route: {Route}, Exchange: {Exchange}, RoutingKey: {RoutingKey}, IsReturn: {IsReturn}, PublishSequenceNumber: {PublishSequenceNumber}",
                messageRoute,
                messageRoute,
                string.Empty,
                ex.IsReturn,
                ex.PublishSequenceNumber);
            throw;
        }
    }
}
