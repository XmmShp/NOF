using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace NOF.Contract;

/// <summary>
/// Represents the result of an operation that does not return a value.
/// Contains information about whether the operation succeeded and, if not, the error details.
/// Instances must be created via <see cref="Success()"/>, <see cref="Success{T}(T)"/>, or <see cref="Fail(string, string)"/>.
/// </summary>
public record Result : IResult<Result>
{
    [JsonConstructor]
    internal Result(bool isSuccess, string? errorCode, string? message, IDictionary<string, string>? extra = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Extra = extra.CreateOrCopy();
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

    public static Result From(IResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is Result typed)
        {
            return typed;
        }

        return other.IsSuccess
            ? Success(other.Extra)
            : new Result(false, other.ErrorCode, other.Message, other.Extra);
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
public record FailResult : IResult<FailResult>
{
    internal FailResult(string errorCode, string message, IDictionary<string, string>? extra = null)
    {
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Extra = extra.CreateOrCopy();
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

    public static FailResult From(IResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is FailResult typed)
        {
            return typed;
        }

        if (other.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Cannot convert a successful '{other.GetType().FullName}' to '{typeof(FailResult).FullName}'.");
        }

        return Result.Fail(other.ErrorCode, other.Message, other.Extra);
    }
}

/// <summary>
/// Represents the result of an operation that returns a value of type <typeparamref name="T"/>.
/// Encapsulates either a success with a value or a failure with error details.
/// Instances must be created via <see cref="Result.Success{T}(T)"/> or <see cref="Result.Fail(string, string)"/>.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public record Result<T> : IResult<Result<T>>
{
    [JsonConstructor]
    internal Result(bool isSuccess, string? errorCode, string? message, T? value, IDictionary<string, string>? extra = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Value = value;
        Extra = extra.CreateOrCopy();
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

    public static Result<T> From(IResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is Result<T> typed)
        {
            return typed;
        }

        if (!other.IsSuccess)
        {
            return new Result<T>(false, other.ErrorCode, other.Message, default, other.Extra);
        }

        if (other.Value is T value)
        {
            return Result.Success(value, other.Extra);
        }

        throw new InvalidOperationException(
            $"Cannot convert a successful '{other.GetType().FullName}' to '{typeof(Result<T>).FullName}'.");
    }

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

public static class ResultProjection
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Result projection is limited to NOF known result shapes.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Result projection is limited to NOF known result shapes.")]
    public static IResult CreateFailure([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type resultType, IResult failure)
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

        if (TryInvokeStaticFrom(resultType, failure, out var projected))
        {
            return projected;
        }

        throw new InvalidOperationException(
            $"Result type '{resultType.FullName}' implements '{typeof(IResult).FullName}' but does not declare a compatible public static From(IResult) method.");
    }

    public static T Require<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(IResult result)
        where T : class, IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is T typed ? typed : (T)CreateFailure(typeof(T), result);
    }

    public static T RequireStruct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(IResult result)
        where T : struct, IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is T typed ? typed : (T)CreateFailure(typeof(T), result);
    }

    public static T RequireCompatible<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(IResult result)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is T typed ? typed : (T)CreateFailure(typeof(T), result);
    }

    public static IResult CreateSuccess([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);

        if (TryInvokeStaticFrom(resultType, Result.Success(), out var projected))
        {
            return projected;
        }

        throw new InvalidOperationException(
            $"Result type '{resultType.FullName}' requires a payload and cannot be created from metadata-only success.");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Result projection only probes the result type for a NOF static From(IResult) entry point.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Result projection only probes the result type for a NOF static From(IResult) entry point.")]
    private static bool TryInvokeStaticFrom([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type resultType, IResult source, [NotNullWhen(true)] out IResult? projected)
    {
        var fromMethod = resultType.GetMethod(
            nameof(Result.From),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(IResult)],
            modifiers: null);

        if (fromMethod is null
            || !typeof(IResult).IsAssignableFrom(fromMethod.ReturnType)
            || !resultType.IsAssignableFrom(fromMethod.ReturnType))
        {
            projected = null;
            return false;
        }

        projected = (IResult?)fromMethod.Invoke(null, [source]);
        if (projected is null)
        {
            throw new InvalidOperationException(
                $"Result type '{resultType.FullName}' returned null from public static From(IResult).");
        }

        return true;
    }
}
