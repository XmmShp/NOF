using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace NOF;

/// <summary>
/// Marker interface for result types that represent the outcome of an operation,
/// either success or failure.
/// </summary>
public interface IResult;

/// <summary>
/// Represents the result of an operation that does not return a value.
/// Contains information about whether the operation succeeded and, if not, the error details.
/// Instances must be created via <see cref="Success()"/>, <see cref="Success{T}(T)"/>, or <see cref="Fail(int, string)"/>.
/// </summary>
public record Result : IResult
{
    [JsonConstructor]
    private Result(bool isSuccess, int errorCode, string? message)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Message = message ?? string.Empty;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error code associated with a failed operation.
    /// Should be zero if <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Gets the human-readable error message describing the failure.
    /// Empty if the operation succeeded.
    /// </summary>
    public string Message { get; }

    #region Static Helpers

    /// <summary>
    /// Defines an implicit conversion from <see cref="FailResult"/> to <see cref="Result"/>.
    /// Enables seamless assignment of failure results.
    /// </summary>
    /// <param name="result">The failure result to convert.</param>
    public static implicit operator Result(FailResult result)
    {
        return new Result(false, result.ErrorCode, result.Message);
    }

    /// <summary>
    /// Creates a new <see cref="FailResult"/> instance representing a failed operation.
    /// </summary>
    /// <param name="errorCode">The numeric error code.</param>
    /// <param name="message">The descriptive error message.</param>
    /// <returns>A <see cref="FailResult"/> representing the failure.</returns>
    public static FailResult Fail(int errorCode, string message)
    {
        return new FailResult(errorCode, message);
    }

    /// <summary>
    /// Creates a successful <see cref="Result"/> with no associated value.
    /// </summary>
    /// <returns>A success result with <see cref="IsSuccess"/> set to <see langword="true"/>.</returns>
    public static Result Success()
    {
        return new Result(true, 0, string.Empty);
    }

    /// <summary>
    /// Creates a successful <see cref="Result{T}"/> containing the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value to wrap.</typeparam>
    /// <param name="value">The value produced by the successful operation.</param>
    /// <returns>A success result containing <paramref name="value"/>.</returns>
    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>
    /// Converts an <see cref="IResult"/> to <see cref="Result"/>.
    /// Handles <see cref="FailResult"/> via implicit conversion, avoiding <see cref="InvalidCastException"/>.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>A <see cref="Result"/> instance.</returns>
    public static Result From(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is FailResult fail ? fail : (Result)result;
    }

    /// <summary>
    /// Converts an <see cref="IResult"/> to <see cref="Result{T}"/>.
    /// Handles <see cref="FailResult"/> via implicit conversion, avoiding <see cref="InvalidCastException"/>.
    /// </summary>
    /// <typeparam name="T">The expected response value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>A <see cref="Result{T}"/> instance.</returns>
    public static Result<T> From<T>(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is FailResult fail ? fail : (Result<T>)result;
    }

    #endregion

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

        return IsSuccess
            ? onSuccess()
            : onFailure(ErrorCode, Message);
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

        if (IsSuccess)
        {
            onSuccess();
        }
        else
        {
            onFailure(ErrorCode, Message);
        }
    }
}

/// <summary>
/// Represents a failed operation result used primarily for construction and implicit conversion.
/// Not intended for direct use in application logic—prefer <see cref="Result"/> or <see cref="Result{T}"/>.
/// Instances must be created via <see cref="Result.Fail(int, string)"/>.
/// </summary>
public record FailResult : IResult
{
    internal FailResult(int errorCode, string message)
    {
        ErrorCode = errorCode;
        Message = message;
    }

    /// <summary>
    /// Gets the numeric error code.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Gets the descriptive error message.
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// Represents the result of an operation that returns a value of type <typeparamref name="T"/>.
/// Encapsulates either a success with a value or a failure with error details.
/// Instances must be created via <see cref="Result.Success{T}(T)"/> or <see cref="Result.Fail(int, string)"/>.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public record Result<T> : IResult
{
    internal Result(T value)
    {
        IsSuccess = true;
        ErrorCode = 0;
        Message = string.Empty;
        Value = value;
    }

    [JsonConstructor]
    private Result(bool isSuccess, int errorCode, string? message, T? value)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Message = message ?? string.Empty;
        Value = value;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// When <see langword="true"/>, <see cref="Value"/> is guaranteed to be non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error code associated with a failed operation.
    /// Should be zero if <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Gets the human-readable error message describing the failure.
    /// Empty if the operation succeeded.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the value produced by the operation if it succeeded.
    /// May be <see langword="null"/> only if the operation failed.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Defines an implicit conversion from <see cref="FailResult"/> to <see cref="Result{T}"/>.
    /// Enables seamless assignment of failure results to generic result types.
    /// </summary>
    /// <param name="result">The failure result to convert.</param>
    public static implicit operator Result<T>(FailResult result)
    {
        return new Result<T>(false, result.ErrorCode, result.Message, default);
    }

    /// <summary>
    /// Defines an implicit conversion from a value of type <typeparamref name="T"/> to <see cref="Result{T}"/>.
    /// Wraps the value in a successful result.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator Result<T>(T value)
        => Result.Success(value);

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

        return IsSuccess
            ? onSuccess(Value!)
            : onFailure(ErrorCode, Message);
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

        if (IsSuccess)
        {
            onSuccess(Value!);
        }
        else
        {
            onFailure(ErrorCode, Message);
        }
    }
}
