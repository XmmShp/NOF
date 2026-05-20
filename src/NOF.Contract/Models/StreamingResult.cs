using System.Diagnostics.CodeAnalysis;

namespace NOF.Contract;

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
        Extra = extra;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    public string ErrorCode { get; }

    public string Message { get; }

    public IAsyncEnumerable<T>? Value { get; }

    public IDictionary<string, string>? Extra { get; }

    public static implicit operator StreamingResult<T>(FailResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new StreamingResult<T>(false, result.ErrorCode, result.Message, null, result.Extra);
    }
}

public static class StreamingResult
{
    public static StreamingResult<T> Success<T>(IAsyncEnumerable<T> value, IDictionary<string, string>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new StreamingResult<T>(true, string.Empty, string.Empty, value, extra);
    }

    public static StreamingResult<T> From<T>(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is FailResult fail ? fail : (StreamingResult<T>)result;
    }
}
