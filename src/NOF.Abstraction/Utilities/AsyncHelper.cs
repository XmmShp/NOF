using System.Runtime.CompilerServices;

namespace NOF.Abstraction;

/// <summary>
/// Helper methods to adapt synchronous operations to asynchronous signatures.
/// Note: This does not make the underlying work cancellable; it only checks the token before starting.
/// </summary>
public static class AsyncHelper
{
    /// <summary>
    /// Wraps a synchronous function into a Task with cancellation token check.
    /// </summary>
    public static Task<T> FromSync<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(func());
    }

    /// <summary>
    /// Wraps a synchronous action into a Task with cancellation token check.
    /// </summary>
    public static Task FromSync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        action();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs a synchronous function on the thread pool honoring the cancellation token pre-check.
    /// </summary>
    public static Task<T> Run<T>(Func<CancellationToken, T> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => func(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Runs a synchronous action on the thread pool honoring the cancellation token pre-check.
    /// </summary>
    public static Task Run(Action<CancellationToken> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            action(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Wraps a synchronous function into a ValueTask with cancellation token check.
    /// </summary>
    public static ValueTask<T> FromSyncValue<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(func());
    }
}
