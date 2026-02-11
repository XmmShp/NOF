namespace NOF.Infrastructure.Core;

/// <summary>
/// Configuration options for the outbox pattern.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// Polling interval.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of messages per polling batch.
    /// Default: 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum retry count for message delivery.
    /// Default: 5.
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    /// Claim lock timeout (prevents permanent deadlock if an instance crashes).
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
