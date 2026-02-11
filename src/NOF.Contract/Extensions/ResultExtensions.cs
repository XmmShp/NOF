namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class NOFContractExtensions
{
    /// <param name="result">The result to match against.</param>
    extension(Result result)
    {
        /// <summary>
        /// Pattern-matches on a <see cref="Result"/>, invoking the appropriate delegate based on success or failure.
        /// Returns a value of type <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the value returned by the match delegates.</typeparam>
        /// <param name="onSuccess">Function to execute if the operation succeeded.</param>
        /// <param name="onFailure">Function to execute if the operation failed; receives error code and message.</param>
        /// <returns>The result of invoking either <paramref name="onSuccess"/> or <paramref name="onFailure"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.
        /// </exception>
        public TResult Match<TResult>(Func<TResult> onSuccess, Func<int, string, TResult> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            return result.IsSuccess
                ? onSuccess()
                : onFailure(result.ErrorCode, result.Message);
        }

        /// <summary>
        /// Pattern-matches on a <see cref="Result"/>, invoking the appropriate action based on success or failure.
        /// Used for side-effect-only operations (returns <see langword="void"/>).
        /// </summary>
        /// <param name="onSuccess">Action to execute if the operation succeeded.</param>
        /// <param name="onFailure">Action to execute if the operation failed; receives error code and message.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.
        /// </exception>
        public void Match(Action onSuccess, Action<int, string> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            if (result.IsSuccess)
            {
                onSuccess();
            }
            else
            {
                onFailure(result.ErrorCode, result.Message);
            }
        }
    }

    /// <param name="result">The result to match against.</param>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    extension<T>(Result<T> result)
    {
        /// <summary>
        /// Pattern-matches on a <see cref="Result{T}"/>, invoking the appropriate delegate based on success or failure.
        /// On success, the contained value is passed to <paramref name="onSuccess"/>.
        /// Returns a value of type <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the value returned by the match delegates.</typeparam>
        /// <param name="onSuccess">Function to execute if the operation succeeded; receives the result value.</param>
        /// <param name="onFailure">Function to execute if the operation failed; receives error code and message.</param>
        /// <returns>The result of invoking either <paramref name="onSuccess"/> or <paramref name="onFailure"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.
        /// </exception>
        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<int, string, TResult> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            return result.IsSuccess
                ? onSuccess(result.Value!)
                : onFailure(result.ErrorCode, result.Message);
        }

        /// <summary>
        /// Pattern-matches on a <see cref="Result{T}"/>, invoking the appropriate action based on success or failure.
        /// On success, the contained value is passed to <paramref name="onSuccess"/>.
        /// Used for side-effect-only operations (returns <see langword="void"/>).
        /// </summary>
        /// <param name="onSuccess">Action to execute if the operation succeeded; receives the result value.</param>
        /// <param name="onFailure">Action to execute if the operation failed; receives error code and message.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.
        /// </exception>
        public void Match(Action<T> onSuccess, Action<int, string> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            if (result.IsSuccess)
            {
                onSuccess(result.Value!);
            }
            else
            {
                onFailure(result.ErrorCode, result.Message);
            }
        }
    }
}
