namespace NOF.Application;

/// <summary>
/// Publishes transient messages to subscribers connected through the same logical backplane.
/// </summary>
public interface IBackplane
{
    /// <summary>
    /// Publishes a message to the specified channel.
    /// </summary>
    /// <typeparam name="TPayload">The message payload type.</typeparam>
    /// <param name="channel">The logical channel name.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes a handler to the specified channel.
    /// </summary>
    /// <typeparam name="TPayload">The payload type accepted by the handler.</typeparam>
    /// <param name="channel">The logical channel name.</param>
    /// <param name="handler">The handler to invoke when a matching message is published.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An async disposable subscription. Disposing it removes the handler from the channel.
    /// </returns>
    ValueTask<IAsyncDisposable> SubscribeAsync<TPayload>(
        string channel,
        Func<TPayload, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default);
}
