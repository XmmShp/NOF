using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// Extension methods for the NOF.Application layer.
/// </summary>
public static partial class NOFApplicationExtensions
{
    extension(Result)
    {
        /// <summary>Creates a failed result from a <see cref="Failure"/> descriptor.</summary>
        /// <param name="failure">The failure descriptor.</param>
        /// <returns>A failed result.</returns>
        public static FailResult Fail(Failure failure)
        {
            return Result.Fail(failure.ErrorCode, failure.Message);
        }

        /// <summary>
        /// Executes an action and converts any thrown <see cref="DomainException"/> into a <see cref="Result"/>.
        /// All other exceptions are rethrown. Optionally logs unexpected exceptions.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="logger">Optional logger to record non-domain exceptions.</param>
        /// <returns>A <see cref="Result"/> representing success or a domain failure.</returns>
        /// <exception cref="Exception">Rethrows any exception that is not a <see cref="DomainException"/>.</exception>
        public static Result Try(Action action, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                action();
                return Result.Success();
            }
            catch (DomainException ex)
            {
                return Result.Fail(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex) when (logger is not null)
            {
                logger.LogError(ex, "An unexpected exception occurred while executing an action.");
                throw;
            }
        }

        /// <summary>
        /// Executes a function and converts any thrown <see cref="DomainException"/> into a <see cref="Result{T}"/>.
        /// All other exceptions are rethrown. Optionally logs unexpected exceptions.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <param name="logger">Optional logger to record non-domain exceptions.</param>
        /// <returns>A <see cref="Result{T}"/> containing the result or a domain failure.</returns>
        /// <exception cref="Exception">Rethrows any exception that is not a <see cref="DomainException"/>.</exception>
        public static Result<T> Try<T>(Func<T> func, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(func);

            try
            {
                var value = func();
                return Result.Success(value);
            }
            catch (DomainException ex)
            {
                return Result.Fail(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex) when (logger is not null)
            {
                logger.LogError(ex, "An unexpected exception occurred while executing a function.");
                throw;
            }
        }

        /// <summary>
        /// Executes an async action and converts any thrown <see cref="DomainException"/> into a <see cref="Result"/>.
        /// All other exceptions are rethrown. Optionally logs unexpected exceptions.
        /// </summary>
        /// <param name="action">The asynchronous action to execute.</param>
        /// <param name="logger">Optional logger to record non-domain exceptions.</param>
        /// <returns>A <see cref="Task{Result}"/> representing success or a domain failure.</returns>
        /// <exception cref="Exception">Rethrows any exception that is not a <see cref="DomainException"/>.</exception>
        public static async Task<Result> TryAsync(Func<Task> action, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                await action().ConfigureAwait(false);
                return Result.Success();
            }
            catch (DomainException ex)
            {
                return Result.Fail(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex) when (logger is not null)
            {
                logger.LogError(ex, "An unexpected exception occurred while executing an async action.");
                throw;
            }
        }

        /// <summary>
        /// Executes an async function and converts any thrown <see cref="DomainException"/> into a <see cref="Result{T}"/>.
        /// All other exceptions are rethrown. Optionally logs unexpected exceptions.
        /// </summary>
        /// <typeparam name="T">The return type of the async function.</typeparam>
        /// <param name="func">The asynchronous function to execute.</param>
        /// <param name="logger">Optional logger to record non-domain exceptions.</param>
        /// <returns>A <see cref="Task"/> containing <see cref="Result{T}"/> the result or a domain failure.</returns>
        /// <exception cref="Exception">Rethrows any exception that is not a <see cref="DomainException"/>.</exception>
        public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(func);

            try
            {
                var value = await func().ConfigureAwait(false);
                return Result.Success(value);
            }
            catch (DomainException ex)
            {
                return Result.Fail(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex) when (logger is not null)
            {
                logger.LogError(ex, "An unexpected exception occurred while executing an async function.");
                throw;
            }
        }
    }
}
