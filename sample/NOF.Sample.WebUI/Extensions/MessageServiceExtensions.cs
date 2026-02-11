using NOF.Contract;

namespace NOF.Sample.WebUI;

public static partial class NOFSampleWebUIExtensions
{
    /// <param name="messageService">The message service to use.</param>
    extension(AntDesign.IMessageService messageService)
    {
        /// <summary>
        /// Displays a success or error message based on the outcome of a <see cref="Result"/>.
        /// Failure messages are always shown. Success messages can be suppressed.
        /// </summary>
        public void ShowMessage(Result result, string? successMessage = null, bool showSuccess = false)
        {
            ArgumentNullException.ThrowIfNull(messageService);
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsSuccess)
            {
                if (showSuccess)
                    messageService.Success(successMessage ?? result.Message);
            }
            else
            {
                messageService.Error(result.Message);
            }
        }

        /// <summary>
        /// Displays a success or error message based on the outcome of a <see cref="Result{T}"/>,
        /// then returns the underlying value if successful, or <see langword="default"/> on failure.
        /// Failure messages are always shown. Success messages can be suppressed.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="result">The operation result.</param>
        /// <param name="successMessage">Optional custom success message.</param>
        /// <param name="showSuccess">If <see langword="false"/>, success message is not displayed.</param>
        /// <returns>The value if successful; otherwise, <see langword="default(T)"/>.</returns>
        public T? UnwrapWithMessage<T>(Result<T> result, string? successMessage = null, bool showSuccess = false)
        {
            ArgumentNullException.ThrowIfNull(messageService);
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsSuccess)
            {
                if (showSuccess)
                {
                    messageService.Success(successMessage ?? result.Message);
                }
                return result.Value;
            }

            messageService.Error(result.Message);
            return default;
        }

        /// <summary>
        /// Displays a success message built from the result value (if successful), or an error message on failure,
        /// then returns the underlying value if successful, or <see langword="default"/> on failure.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="result">The operation result.</param>
        /// <param name="successMessageFactory">Function to generate a success message from the result value.</param>
        /// <param name="showSuccess">If <see langword="false"/>, success message is not displayed.</param>
        /// <returns>The value if successful; otherwise, <see langword="default(T)"/>.</returns>
        public T? UnwrapWithMessage<T>(Result<T> result, Func<T, string> successMessageFactory, bool showSuccess = false)
        {
            ArgumentNullException.ThrowIfNull(messageService);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(successMessageFactory);

            if (result.IsSuccess)
            {
                var message = successMessageFactory(result.Value);
                if (showSuccess)
                {
                    messageService.Success(message);
                }
                return result.Value;
            }

            messageService.Error(result.Message);
            return default;
        }
    }
}
