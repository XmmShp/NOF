using System.Diagnostics.CodeAnalysis;

namespace NOF.Contract;

internal static class RpcResultHeaders
{
    public static IReadOnlyDictionary<string, string?> Empty { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string?> Create(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        if (headers is null)
        {
            return Empty;
        }

        var copied = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers)
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
    object? Body { get; }

    int? StatusCode { get; }

    IReadOnlyDictionary<string, string?> Headers { get; }
}

internal sealed record RpcEnvelopeResult(
    object? Body,
    int? StatusCode,
    IReadOnlyDictionary<string, string?> Headers) : IRpcResult;

/// <summary>
/// Represents the in-memory RPC response envelope of an RPC operation with a payload.
/// </summary>
public sealed record RpcResult<T> : IRpcResult
{
    internal RpcResult(
        T? value,
        object? body = null,
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
    {
        Value = value;
        _body = body;
        StatusCode = statusCode;
        Headers = RpcResultHeaders.Create(headers);
    }

    private readonly object? _body;

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => !StatusCode.HasValue || StatusCode.Value is >= 200 and < 300;

    public T? Value { get; }

    public object? Body => _body ?? Value;

    public int? StatusCode { get; }

    public IReadOnlyDictionary<string, string?> Headers { get; }
}

public static class RpcResults
{
    public static RpcResult<T> Success<T>(
        T value,
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => new(value, statusCode: statusCode, headers: headers);

    public static RpcResult<T> Response<T>(
        T? value,
        object? body = null,
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => new(value, body, statusCode, headers);

    public static IRpcResult Response(
        object? body = null,
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => new RpcEnvelopeResult(body, statusCode, RpcResultHeaders.Create(headers));

    public static IRpcResult Fail(
        int statusCode,
        object? body = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => new RpcEnvelopeResult(body, statusCode, RpcResultHeaders.Create(headers));

    public static RpcResult<T> FromFailure<T>(
        object? body = null,
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => new(default, body, statusCode, headers);

    public static RpcResult<T> From<T>(IRpcResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result is RpcResult<T> typedResult)
        {
            return typedResult;
        }

        return new RpcResult<T>(default, result.Body, result.StatusCode, result.Headers);
    }
}
