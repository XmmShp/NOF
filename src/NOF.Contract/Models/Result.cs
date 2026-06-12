using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace NOF.Contract;

internal static class ResultExtra
{
    public static Dictionary<string, string> Create(IDictionary<string, string>? extra)
    {
        return extra is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(extra);
    }
}

/// <summary>
/// Marker interface for result types that represent the outcome of an operation,
/// either success or failure.
/// </summary>
public interface IResult
{
    /// <summary>Indicates whether the operation succeeded.</summary>
    bool IsSuccess { get; }

    /// <summary>Error code when failed; empty when succeeded.</summary>
    string ErrorCode { get; }

    /// <summary>Human-readable message; empty when succeeded.</summary>
    string Message { get; }

    /// <summary>The success payload when available; otherwise <see langword="null"/>.</summary>
    object? Value { get; }

    /// <summary>
    /// Additional metadata to accompany the result.
    /// </summary>
    IDictionary<string, string> Extra { get; }
}

/// <summary>
/// Represents the result of an operation that does not return a value.
/// Contains information about whether the operation succeeded and, if not, the error details.
/// Instances must be created via <see cref="Success()"/>, <see cref="Success{T}(T)"/>, or <see cref="Fail(string, string)"/>.
/// </summary>
public record Result : IResult
{
    [JsonConstructor]
    internal Result(bool isSuccess, string? errorCode, string? message, IDictionary<string, string>? extra = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Extra = ResultExtra.Create(extra);
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error code associated with a failed operation.
    /// Should be empty if <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the human-readable error message describing the failure.
    /// Empty if the operation succeeded.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Additional metadata to accompany the result.
    /// </summary>
    public IDictionary<string, string> Extra { get; }

    object? IResult.Value => null;

    #region Static Helpers

    /// <summary>
    /// Defines an implicit conversion from <see cref="FailResult"/> to <see cref="Result"/>.
    /// Enables seamless assignment of failure results.
    /// </summary>
    /// <param name="result">The failure result to convert.</param>
    public static implicit operator Result(FailResult result)
    {
        return new Result(false, result.ErrorCode, result.Message, result.Extra);
    }

    /// <summary>
    /// Creates a new <see cref="FailResult"/> instance representing a failed operation.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The descriptive error message.</param>
    /// <returns>A <see cref="FailResult"/> representing the failure.</returns>
    public static FailResult Fail(string errorCode, string message, IDictionary<string, string>? extra = null)
    {
        return new FailResult(errorCode, message, extra);
    }

    /// <summary>
    /// Creates a successful <see cref="Result"/> with no associated value.
    /// </summary>
    /// <returns>A success result with <see cref="IsSuccess"/> set to <see langword="true"/>.</returns>
    public static Result Success(IDictionary<string, string>? extra = null)
    {
        return new Result(true, string.Empty, string.Empty, extra);
    }

    /// <summary>
    /// Creates a successful <see cref="Result{T}"/> containing the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value to wrap.</typeparam>
    /// <param name="value">The value produced by the successful operation.</param>
    /// <returns>A success result containing <paramref name="value"/>.</returns>
    public static Result<T> Success<T>(T value, IDictionary<string, string>? extra = null)
    {
        return new Result<T>(true, string.Empty, string.Empty, value, extra);
    }

    /// <summary>
    /// Creates a successful <see cref="StreamingResult{T}"/> containing the specified async stream.
    /// </summary>
    public static StreamingResult<T> Stream<T>(IAsyncEnumerable<T> value, IDictionary<string, string>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new StreamingResult<T>(true, string.Empty, string.Empty, value, extra);
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
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<string, string, TResult> onFailure)
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
    public void Match(Action onSuccess, Action<string, string> onFailure)
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
/// Instances must be created via <see cref="Result.Fail(string, string)"/>.
/// </summary>
public record FailResult : IResult
{
    internal FailResult(string errorCode, string message, IDictionary<string, string>? extra = null)
    {
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Extra = ResultExtra.Create(extra);
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the descriptive error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Always false for failed result.
    /// </summary>
    public bool IsSuccess => false;

    /// <summary>
    /// Additional metadata to accompany the result.
    /// </summary>
    public IDictionary<string, string> Extra { get; }

    public object? Value => null;
}

/// <summary>
/// Represents the result of an operation that returns a value of type <typeparamref name="T"/>.
/// Encapsulates either a success with a value or a failure with error details.
/// Instances must be created via <see cref="Result.Success{T}(T)"/> or <see cref="Result.Fail(string, string)"/>.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public record Result<T> : IResult
{
    [JsonConstructor]
    internal Result(bool isSuccess, string? errorCode, string? message, T? value, IDictionary<string, string>? extra = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Value = value;
        Extra = ResultExtra.Create(extra);
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// When <see langword="true"/>, <see cref="Value"/> is guaranteed to be non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error code associated with a failed operation.
    /// Should be empty if <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public string ErrorCode { get; }

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

    object? IResult.Value => Value;

    /// <summary>
    /// Additional metadata to accompany the result.
    /// </summary>
    public IDictionary<string, string> Extra { get; }

    /// <summary>
    /// Defines an implicit conversion from <see cref="FailResult"/> to <see cref="Result{T}"/>.
    /// Enables seamless assignment of failure results to generic result types.
    /// </summary>
    /// <param name="result">The failure result to convert.</param>
    public static implicit operator Result<T>(FailResult result)
    {
        return new Result<T>(false, result.ErrorCode, result.Message, default, result.Extra);
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
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, string, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess(Value)
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
    public void Match(Action<T> onSuccess, Action<string, string> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (IsSuccess)
        {
            onSuccess(Value);
        }
        else
        {
            onFailure(ErrorCode, Message);
        }
    }
}

/// <summary>
/// Represents the outcome of a streaming RPC operation.
/// </summary>
public sealed record StreamingResult<T> : IResult
{
    internal StreamingResult(bool isSuccess, string? errorCode, string? message, IAsyncEnumerable<T>? value, IDictionary<string, string>? extra = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Value = value;
        Extra = ResultExtra.Create(extra);
    }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    public string ErrorCode { get; }

    public string Message { get; }

    public IAsyncEnumerable<T>? Value { get; }

    object? IResult.Value => Value;

    public IDictionary<string, string> Extra { get; }

    public static implicit operator StreamingResult<T>(FailResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new StreamingResult<T>(false, result.ErrorCode, result.Message, null, result.Extra);
    }
}

public static class StreamingResult
{
    public static StreamingResult<T> From<T>(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result is StreamingResult<T> typedResult)
        {
            return typedResult;
        }

        if (!result.IsSuccess)
        {
            return new StreamingResult<T>(false, result.ErrorCode, result.Message, null, result.Extra);
        }

        throw new InvalidOperationException($"Cannot convert a successful '{result.GetType().FullName}' to '{typeof(StreamingResult<T>).FullName}'.");
    }
}

public static class ResultProjection
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Result projection is limited to NOF known result shapes.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Result projection is limited to NOF known result shapes.")]
    public static IResult CreateFailure(Type resultType, IResult failure)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        ArgumentNullException.ThrowIfNull(failure);

        if (!typeof(IResult).IsAssignableFrom(resultType))
        {
            throw new InvalidOperationException(
                $"Cannot create a failure result for non-result type '{resultType.FullName}'.");
        }

        if (resultType.IsInstanceOfType(failure))
        {
            return failure;
        }

        if (resultType == typeof(Result))
        {
            return new Result(false, failure.ErrorCode, failure.Message, failure.Extra);
        }

        if (resultType == typeof(FailResult))
        {
            return Result.Fail(failure.ErrorCode, failure.Message, failure.Extra);
        }

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var method = typeof(ResultProjection)
                .GetMethod(nameof(CreateTypedFailureResult), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(resultType.GetGenericArguments()[0]);
            return (IResult)method.Invoke(null, [failure])!;
        }

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(StreamingResult<>))
        {
            var method = typeof(ResultProjection)
                .GetMethod(nameof(CreateTypedStreamingFailureResult), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(resultType.GetGenericArguments()[0]);
            return (IResult)method.Invoke(null, [failure])!;
        }

        throw new InvalidOperationException(
            $"Result type '{resultType.FullName}' implements '{typeof(IResult).FullName}' but is not supported for failure projection.");
    }

    public static T Require<T>(IResult result)
        where T : class, IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is T typed ? typed : (T)CreateFailure(typeof(T), result);
    }

    public static T RequireStruct<T>(IResult result)
        where T : struct, IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is T typed ? typed : (T)CreateFailure(typeof(T), result);
    }

    public static T RequireCompatible<T>(IResult result)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is T typed ? typed : (T)CreateFailure(typeof(T), result);
    }

    public static IResult CreateSuccess(Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);

        if (resultType == typeof(Result))
        {
            return Result.Success();
        }

        throw new InvalidOperationException(
            $"Result type '{resultType.FullName}' requires a payload and cannot be created from metadata-only success.");
    }

    private static Result<TItem> CreateTypedFailureResult<TItem>(IResult failure)
        => new(false, failure.ErrorCode, failure.Message, default, failure.Extra);

    private static StreamingResult<TItem> CreateTypedStreamingFailureResult<TItem>(IResult failure)
        => new(false, failure.ErrorCode, failure.Message, null, failure.Extra);
}
