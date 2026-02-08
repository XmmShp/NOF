using System.Diagnostics.CodeAnalysis;

namespace NOF;

/// <summary>
/// Marker interface for result types that represent the outcome of an operation,
/// either success or failure.
/// </summary>
public interface IResult;

/// <summary>
/// Represents the result of an operation that does not return a value.
/// Contains information about whether the operation succeeded and, if not, the error details.
/// </summary>
public record Result : IResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error code associated with a failed operation.
    /// Should be zero if <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public int ErrorCode { get; init; }

    /// <summary>
    /// Gets the human-readable error message describing the failure.
    /// Empty if the operation succeeded.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    #region Static Helpers

    /// <summary>
    /// Defines an implicit conversion from <see cref="FailResult"/> to <see cref="Result"/>.
    /// Enables seamless assignment of failure results.
    /// </summary>
    /// <param name="result">The failure result to convert.</param>
    public static implicit operator Result(FailResult result)
    {
        return new Result
        {
            IsSuccess = false,
            ErrorCode = result.ErrorCode,
            Message = result.Message
        };
    }

    /// <summary>
    /// Creates a new <see cref="FailResult"/> instance representing a failed operation.
    /// </summary>
    /// <param name="errorCode">The numeric error code.</param>
    /// <param name="message">The descriptive error message.</param>
    /// <returns>A <see cref="FailResult"/> representing the failure.</returns>
    public static FailResult Fail(int errorCode, string message)
    {
        return new FailResult
        {
            ErrorCode = errorCode,
            Message = message
        };
    }

    /// <summary>
    /// Creates a successful <see cref="Result"/> with no associated value.
    /// </summary>
    /// <returns>A success result with <see cref="IsSuccess"/> set to <see langword="true"/>.</returns>
    public static Result Success()
    {
        return new Result
        {
            IsSuccess = true,
            ErrorCode = 0
        };
    }

    /// <summary>
    /// Creates a successful <see cref="Result{T}"/> containing the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value to wrap.</typeparam>
    /// <param name="value">The value produced by the successful operation.</param>
    /// <returns>A success result containing <paramref name="value"/>.</returns>
    public static Result<T> Success<T>(T value)
    {
        return new Result<T>
        {
            IsSuccess = true,
            ErrorCode = 0,
            Message = string.Empty,
            Value = value
        };
    }

    #endregion
}

/// <summary>
/// Represents a failed operation result used primarily for construction and implicit conversion.
/// Not intended for direct use in application logic—prefer <see cref="Result"/> or <see cref="Result{T}"/>.
/// </summary>
public record FailResult : IResult
{
    /// <summary>
    /// Gets the numeric error code. Required.
    /// </summary>
    public required int ErrorCode { get; init; }

    /// <summary>
    /// Gets the descriptive error message. Required.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Represents the result of an operation that returns a value of type <typeparamref name="T"/>.
/// Encapsulates either a success with a value or a failure with error details.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public record Result<T> : IResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// When <see langword="true"/>, <see cref="Value"/> is guaranteed to be non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error code associated with a failed operation.
    /// Should be zero if <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public int ErrorCode { get; init; }

    /// <summary>
    /// Gets the human-readable error message describing the failure.
    /// Empty if the operation succeeded.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value produced by the operation if it succeeded.
    /// May be <see langword="null"/> only if the operation failed.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Defines an implicit conversion from <see cref="FailResult"/> to <see cref="Result{T}"/>.
    /// Enables seamless assignment of failure results to generic result types.
    /// </summary>
    /// <param name="result">The failure result to convert.</param>
    public static implicit operator Result<T>(FailResult result)
    {
        return new Result<T>
        {
            IsSuccess = false,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            Value = default
        };
    }

    /// <summary>
    /// Defines an implicit conversion from a value of type <typeparamref name="T"/> to <see cref="Result{T}"/>.
    /// Wraps the value in a successful result.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator Result<T>(T value)
        => Result.Success(value);
}
