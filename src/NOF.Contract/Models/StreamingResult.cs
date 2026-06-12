using System.Diagnostics.CodeAnalysis;

namespace NOF.Contract;

/// <summary>
/// Represents the outcome of a streaming RPC operation.
/// </summary>
public sealed record StreamingResult<T> : IResult<StreamingResult<T>>
{
    internal StreamingResult(bool isSuccess, string? errorCode, string? message, IAsyncEnumerable<T>? value, IDictionary<string, string>? extra = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode ?? string.Empty;
        Message = message ?? string.Empty;
        Value = value;
        Extra = extra.CreateOrCopy();
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

    public static StreamingResult<T> From(IResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is StreamingResult<T> typed)
        {
            return typed;
        }

        if (!other.IsSuccess)
        {
            return new StreamingResult<T>(false, other.ErrorCode, other.Message, null, other.Extra);
        }

        if (other.Value is IAsyncEnumerable<T> stream)
        {
            return new StreamingResult<T>(true, string.Empty, string.Empty, stream, other.Extra);
        }

        if (other.Value is IEnumerable<T> enumerable)
        {
            return new StreamingResult<T>(true, string.Empty, string.Empty, enumerable.ToAsyncEnumerable(), other.Extra);
        }

        if (other.Value is T value)
        {
            return new StreamingResult<T>(true, string.Empty, string.Empty, new[] { value }.ToAsyncEnumerable(), other.Extra);
        }

        throw new InvalidOperationException(
            $"Cannot convert a successful '{other.GetType().FullName}' to '{typeof(StreamingResult<T>).FullName}'.");
    }
}
