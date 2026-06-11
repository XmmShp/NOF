namespace NOF.Contract;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

internal static class RpcResultMetadatas
{
    public static IReadOnlyDictionary<string, string?> Empty { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string?> Create(IEnumerable<KeyValuePair<string, string?>>? metadatas)
    {
        if (metadatas is null)
        {
            return Empty;
        }

        var copied = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadatas)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            copied[key] = value;
        }

        return copied.Count == 0
            ? Empty
            : copied;
    }
}

public interface IRpcResult
{
    bool IsSuccess { get; }

    object? Body { get; }

    IReadOnlyDictionary<string, string?> Metadatas { get; }
}

internal sealed class RpcEnvelopeResult : IRpcResult
{
    public RpcEnvelopeResult(
        bool isSuccess,
        object? body,
        IReadOnlyDictionary<string, string?> metadatas)
    {
        IsSuccess = isSuccess;
        Body = body;
        Metadatas = metadatas;
    }

    public bool IsSuccess { get; }

    public object? Body { get; }

    public IReadOnlyDictionary<string, string?> Metadatas { get; }
}

/// <summary>
/// Represents the in-memory RPC response envelope of an RPC operation with a payload.
/// </summary>
public abstract class RpcResult<T> : IRpcResult
{
    protected RpcResult(
        bool isSuccess,
        object? body = null,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
    {
        IsSuccess = isSuccess;
        Body = body;
        Metadatas = RpcResultMetadatas.Create(metadatas);
    }

    public bool IsSuccess { get; }

    public object? Body { get; }

    public IReadOnlyDictionary<string, string?> Metadatas { get; }
}

internal sealed class RpcTypedResult<T> : RpcResult<T>
{
    public RpcTypedResult(
        bool isSuccess,
        object? body = null,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        : base(isSuccess, body, metadatas)
    {
    }
}

public static class RpcResults
{
    public static RpcResult<T> Success<T>(
        T value,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => new RpcTypedResult<T>(true, value, metadatas);

    public static RpcResult<T> Response<T>(
        bool isSuccess,
        object? body = null,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => new RpcTypedResult<T>(isSuccess, body, metadatas);

    public static IRpcResult Response(
        bool isSuccess = true,
        object? body = null,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => new RpcEnvelopeResult(isSuccess, body, RpcResultMetadatas.Create(metadatas));

    public static IRpcResult Fail(IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => new RpcEnvelopeResult(false, null, RpcResultMetadatas.Create(metadatas));

    public static RpcResult<T> FromFailure<T>(IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => new RpcTypedResult<T>(false, null, metadatas);

    public static RpcResult<T> BusinessFailure<T>(
        IResult failure,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new RpcTypedResult<T>(true, CreateBusinessFailureBody(typeof(T), failure), metadatas);
    }

    public static IRpcResult BusinessFailure(
        Type resultType,
        IResult failure,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        ArgumentNullException.ThrowIfNull(failure);
        return new RpcEnvelopeResult(true, CreateBusinessFailureBody(resultType, failure), RpcResultMetadatas.Create(metadatas));
    }

    public static RpcResult<T> From<T>(IRpcResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result is RpcResult<T> typedResult)
        {
            return typedResult;
        }

        return new RpcTypedResult<T>(result.IsSuccess, result.Body, result.Metadatas);
    }

    public static T RequireBody<T>(IRpcResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Body is T body)
        {
            return body;
        }

        if (result.Body is null)
        {
            throw new InvalidOperationException("RPC call returned an empty body.");
        }

        if (result.Body is IResult businessResult)
        {
            return ProjectBusinessResult<T>(businessResult);
        }

        throw new InvalidOperationException(
            $"RPC call returned body type '{result.Body.GetType().FullName}', expected '{typeof(T).FullName}'.");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Result projection is limited to NOF known result shapes.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Result projection is limited to NOF known result shapes.")]
    internal static object CreateBusinessFailureBody(Type resultType, IResult failure)
    {
        if (!typeof(IResult).IsAssignableFrom(resultType))
        {
            throw new InvalidOperationException(
                $"Cannot create a business failure body for non-result type '{resultType.FullName}'.");
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
            var method = typeof(RpcResults)
                .GetMethod(nameof(CreateTypedFailureResult), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(resultType.GetGenericArguments()[0]);
            return method.Invoke(null, [failure])!;
        }

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(StreamingResult<>))
        {
            var method = typeof(RpcResults)
                .GetMethod(nameof(CreateTypedStreamingFailureResult), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(resultType.GetGenericArguments()[0]);
            return method.Invoke(null, [failure])!;
        }

        throw new InvalidOperationException(
            $"Result type '{resultType.FullName}' is assignable to '{typeof(IResult).FullName}' but is not supported for projection.");
    }

    private static Result<TItem> CreateTypedFailureResult<TItem>(IResult failure)
        => new(false, failure.ErrorCode, failure.Message, default, failure.Extra);

    private static StreamingResult<TItem> CreateTypedStreamingFailureResult<TItem>(IResult failure)
        => new(false, failure.ErrorCode, failure.Message, null, failure.Extra);

    private static T ProjectBusinessResult<T>(IResult businessResult)
    {
        if (businessResult is T typedResult)
        {
            return typedResult;
        }

        if (!typeof(IResult).IsAssignableFrom(typeof(T)))
        {
            throw new InvalidOperationException(
                $"RPC call returned business result '{businessResult.GetType().FullName}', expected '{typeof(T).FullName}'.");
        }

        return (T)CreateBusinessFailureBody(typeof(T), businessResult);
    }
}
