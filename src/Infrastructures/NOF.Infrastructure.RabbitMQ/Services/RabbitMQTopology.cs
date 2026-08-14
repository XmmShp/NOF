using RabbitMQ.Client;

namespace NOF.Infrastructure.RabbitMQ;

internal static class RabbitMQTopology
{
    public const string UnroutableExchangeName = "nof.io-vii.com.unroutable.exchange";
    public const string UnroutableQueueName = "nof.io-vii.com.unroutable.queue";
    public const string DeadLetterExchangeName = "nof.io-vii.com.dead-letter.exchange";
    public const string DeadLetterQueueName = "nof.io-vii.com.dead-letter.queue";
    public const string OriginalExchangeHeader = "nof-original-exchange";
    public const string OriginalRoutingKeyHeader = "nof-original-routing-key";

    public static string BuildCommandQueueName(string messageRoute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageRoute);
        return messageRoute;
    }

    public static IDictionary<string, object?> BuildBusinessExchangeArguments()
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["alternate-exchange"] = UnroutableExchangeName
        };

    public static IDictionary<string, object?> BuildBusinessQueueArguments()
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x-dead-letter-exchange"] = DeadLetterExchangeName
        };

    public static void AddOriginalRouteHeaders(
        IDictionary<string, object?> headers,
        string exchange,
        string routingKey)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        headers[OriginalExchangeHeader] = exchange;
        headers[OriginalRoutingKeyHeader] = routingKey;
    }

    public static async Task DeclareSystemTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await channel.ExchangeDeclareAsync(
            exchange: UnroutableExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: UnroutableQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: UnroutableQueueName,
            exchange: UnroutableExchangeName,
            routingKey: "#",
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: DeadLetterExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: DeadLetterQueueName,
            exchange: DeadLetterExchangeName,
            routingKey: "#",
            cancellationToken: cancellationToken);
    }
}
