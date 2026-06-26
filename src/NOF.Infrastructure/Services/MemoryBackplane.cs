using NOF.Application;
using System.Collections.Concurrent;

namespace NOF.Infrastructure;

/// <summary>
/// In-memory backplane implementation scoped to a single NOF host process.
/// </summary>
public sealed class MemoryBackplane : IBackplane
{
    private readonly MemoryBackplaneState _state;

    public MemoryBackplane()
        : this(new MemoryBackplaneState())
    {
    }

    public MemoryBackplane(MemoryBackplaneState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public async ValueTask PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_state.Channels.TryGetValue(channel, out var subscriptions) || subscriptions.IsEmpty)
        {
            return;
        }

        foreach (var subscription in subscriptions.Values.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await subscription.DispatchAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<IAsyncDisposable> SubscribeAsync<TPayload>(
        string channel,
        Func<TPayload, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        var subscriptionId = Guid.NewGuid();
        var channelSubscriptions = _state.Channels.GetOrAdd(
            channel,
            static _ => new ConcurrentDictionary<Guid, Subscription>());
        var subscription = new Subscription<TPayload>(_state, channel, subscriptionId, handler);
        channelSubscriptions[subscriptionId] = subscription;
        return ValueTask.FromResult<IAsyncDisposable>(subscription);
    }

    internal abstract class Subscription : IAsyncDisposable
    {
        public abstract ValueTask DispatchAsync(object payload, CancellationToken cancellationToken);

        public abstract ValueTask DisposeAsync();
    }

    private sealed class Subscription<TPayload> : Subscription
    {
        private readonly MemoryBackplaneState _state;
        private readonly string _channel;
        private readonly Guid _subscriptionId;
        private readonly Func<TPayload, CancellationToken, ValueTask> _handler;
        private int _disposed;

        public Subscription(
            MemoryBackplaneState state,
            string channel,
            Guid subscriptionId,
            Func<TPayload, CancellationToken, ValueTask> handler)
        {
            _state = state;
            _channel = channel;
            _subscriptionId = subscriptionId;
            _handler = handler;
        }

        public override ValueTask DispatchAsync(object payload, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _disposed) == 1 || payload is not TPayload typedPayload)
            {
                return ValueTask.CompletedTask;
            }

            return _handler(typedPayload, cancellationToken);
        }

        public override ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return ValueTask.CompletedTask;
            }

            if (_state.Channels.TryGetValue(_channel, out var subscriptions))
            {
                subscriptions.TryRemove(_subscriptionId, out _);
                if (subscriptions.IsEmpty)
                {
                    _state.Channels.TryRemove(_channel, out _);
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
