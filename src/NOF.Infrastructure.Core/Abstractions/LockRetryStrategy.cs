namespace NOF.Infrastructure.Core;

/// <summary>
/// Defines a strategy for retrying lock acquisition with backoff.
/// </summary>
public interface ICacheLockRetryStrategy
{
    /// <summary>
    /// Gets the initial retry delay in milliseconds.
    /// </summary>
    int InitialDelayMs { get; }

    /// <summary>
    /// Calculates the next retry delay based on the current attempt.
    /// </summary>
    /// <param name="currentDelayMs">The current delay in milliseconds.</param>
    /// <param name="attemptNumber">The current attempt number (0-based).</param>
    /// <param name="isForTryAcquire">Whether this is for TryAcquireLock (may have different max delay).</param>
    /// <returns>The next delay in milliseconds to wait before retrying.</returns>
    int GetNextDelay(int currentDelayMs, int attemptNumber, bool isForTryAcquire);
}

/// <summary>
/// Default exponential backoff retry strategy with jitter.
/// </summary>
public sealed class ExponentialBackoffCacheLockRetryStrategy : ICacheLockRetryStrategy
{
    /// <summary>
    /// Gets or sets the initial retry delay in milliseconds.
    /// Default is 10 milliseconds.
    /// </summary>
    public int InitialDelayMs { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum retry delay for AcquireLock operations.
    /// Default is 1000 milliseconds.
    /// </summary>
    public int MaxDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum retry delay for TryAcquireLock operations.
    /// Default is 500 milliseconds.
    /// </summary>
    public int MaxDelayForTryAcquireMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the backoff multiplier.
    /// Default is 2.0 (exponential backoff).
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the jitter factor (0.0 to 1.0).
    /// Jitter range will be [0, delay * jitterFactor].
    /// Default is 0.5 (up to 50% jitter).
    /// </summary>
    public double JitterFactor { get; set; } = 0.5;

    /// <inheritdoc />
    public int GetNextDelay(int currentDelayMs, int attemptNumber, bool isForTryAcquire)
    {
        var maxDelay = isForTryAcquire ? MaxDelayForTryAcquireMs : MaxDelayMs;

        // Calculate next delay with backoff
        var nextDelay = (int)Math.Min(currentDelayMs * BackoffMultiplier, maxDelay);

        // Add jitter
        var jitter = (int)(nextDelay * JitterFactor);
        var delayWithJitter = nextDelay + Random.Shared.Next(0, jitter);

        return Math.Min(delayWithJitter, maxDelay + jitter);
    }
}
