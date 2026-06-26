using NOF.Application;
using StackExchange.Redis;

namespace NOF.Infrastructure.StackExchangeRedis;

/// <summary>
/// Redis-backed backplane implementation using StackExchange.Redis pub/sub.
/// </summary>
public sealed class RedisBackplane : IBackplane
{
    private readonly ISubscriber _subscriber;
    private readonly IObjectSerializer _serializer;

    public RedisBackplane(IConnectionMultiplexer connectionMultiplexer, IObjectSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(serializer);

        _subscriber = connectionMultiplexer.GetSubscriber();
        _serializer = serializer;
    }

    public async ValueTask PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        var redisChannel = RedisChannel.Literal(channel);
        var payloadType = typeof(TPayload);
        var envelope = new RedisBackplaneEnvelope(
            payloadType.AssemblyQualifiedName ?? payloadType.FullName ?? payloadType.Name,
            _serializer.Serialize(payload, payloadType).ToArray());
        var message = _serializer.Serialize(envelope, typeof(RedisBackplaneEnvelope));

        await _subscriber.PublishAsync(redisChannel, message.ToArray()).ConfigureAwait(false);
    }

    public async ValueTask<IAsyncDisposable> SubscribeAsync<TPayload>(
        string channel,
        Func<TPayload, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        var redisChannel = RedisChannel.Literal(channel);
        var expectedPayloadTypeName = typeof(TPayload).AssemblyQualifiedName ?? typeof(TPayload).FullName ?? typeof(TPayload).Name;
        Action<RedisChannel, RedisValue> redisHandler = (_, value) =>
        {
            var envelope = _serializer.Deserialize<RedisBackplaneEnvelope>((byte[])value!);
            if (envelope is null || !string.Equals(envelope.PayloadType, expectedPayloadTypeName, StringComparison.Ordinal))
            {
                return;
            }

            var payload = _serializer.Deserialize<TPayload>(envelope.Payload);
            if (payload is TPayload typedPayload)
            {
                handler(typedPayload, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
        };

        await _subscriber.SubscribeAsync(redisChannel, redisHandler).ConfigureAwait(false);
        return new RedisBackplaneSubscription(_subscriber, redisChannel, redisHandler);
    }

    private sealed class RedisBackplaneSubscription : IAsyncDisposable
    {
        private readonly ISubscriber _subscriber;
        private readonly RedisChannel _channel;
        private readonly Action<RedisChannel, RedisValue> _handler;
        private int _disposed;

        public RedisBackplaneSubscription(
            ISubscriber subscriber,
            RedisChannel channel,
            Action<RedisChannel, RedisValue> handler)
        {
            _subscriber = subscriber;
            _channel = channel;
            _handler = handler;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            await _subscriber.UnsubscribeAsync(_channel, _handler).ConfigureAwait(false);
        }
    }

    private sealed record RedisBackplaneEnvelope(string PayloadType, byte[] Payload);
}
