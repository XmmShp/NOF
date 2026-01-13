namespace NOF;

/// <summary>
/// Defines a strategy for retrying lock acquisition with backoff.
/// </summary>
public interface ILockRetryStrategy
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
