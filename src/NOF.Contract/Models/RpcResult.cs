using System.Diagnostics.CodeAnalysis;
namespace NOF.Contract;

public interface IRpcResult
{
    bool TryGetTransportFailure([NotNullWhen(true)] out IResult? result);
}

public sealed record RpcFailureResult : IRpcResult
{
    internal RpcFailureResult(IResult transportFailure)
    {
        ArgumentNullException.ThrowIfNull(transportFailure);
        if (transportFailure.IsSuccess)
        {
            throw new InvalidOperationException("Transport failure result must be unsuccessful.");
        }

        TransportFailure = transportFailure as FailResult
            ?? Result.Fail(transportFailure.ErrorCode, transportFailure.Message, transportFailure.Extra);
    }

    public FailResult TransportFailure { get; }

    public bool TryGetTransportFailure([NotNullWhen(true)] out IResult? result)
    {
        result = TransportFailure;
        return true;
    }
}

/// <summary>
/// Represents the in-memory RPC outcome of an RPC operation with a payload.
/// </summary>
public sealed record RpcResult<T> : IRpcResult
{
    internal RpcResult(T? value)
    {
        Value = value;
    }

    internal RpcResult(IRpcResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    public IRpcResult? Result { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Result is null;

    public T? Value { get; }

    public string ErrorCode => Result is null ? string.Empty : RpcResults.ToFailureResult(Result).ErrorCode;

    public string Message => Result is null ? string.Empty : RpcResults.ToFailureResult(Result).Message;

    public IDictionary<string, string> Extra => Result is null ? ResultExtra.Create(null) : RpcResults.ToFailureResult(Result).Extra;

    public static implicit operator Result<T>(RpcResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.TryGetTransportFailure(out var transportFailure))
        {
            return NOF.Contract.Result.From<T>(transportFailure);
        }

        return NOF.Contract.Result.Success(result.Value!);
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, string, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess(Value)
            : onFailure(ErrorCode, Message);
    }

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

    public bool TryGetTransportFailure([NotNullWhen(true)] out IResult? result)
    {
        if (Result is not null)
        {
            return Result.TryGetTransportFailure(out result);
        }

        result = null;
        return false;
    }
}

public static class RpcResults
{
    public static RpcResult<T> Success<T>(T value, IDictionary<string, string>? extra = null)
    {
        if (extra is { Count: > 0 })
        {
            throw new InvalidOperationException("Transport metadata cannot be attached to a successful in-memory RpcResult. Convert it at the rider boundary instead.");
        }

        return new RpcResult<T>(value);
    }

    public static IRpcResult Fail(string errorCode, string message, IDictionary<string, string>? extra = null)
    {
        return new RpcFailureResult(Result.Fail(errorCode, message, extra));
    }

    public static RpcResult<T> FromFailure<T>(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return new RpcResult<T>(new RpcFailureResult(result));
        }

        throw new InvalidOperationException($"Cannot convert a successful '{result.GetType().FullName}' to '{typeof(RpcResult<T>).FullName}'.");
    }

    public static FailResult ToFailureResult(IRpcResult? result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.TryGetTransportFailure(out var transportFailure))
        {
            return transportFailure as FailResult
                ?? Result.Fail(transportFailure.ErrorCode, transportFailure.Message, transportFailure.Extra);
        }

        throw new InvalidOperationException($"'{result.GetType().FullName}' does not carry a transport failure.");
    }

    public static RpcResult<T> From<T>(IRpcResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result is RpcResult<T> typedResult)
        {
            return typedResult;
        }

        if (result.TryGetTransportFailure(out var transportFailure))
        {
            return FromFailure<T>(transportFailure);
        }

        throw new InvalidOperationException($"Cannot convert '{result.GetType().FullName}' to '{typeof(RpcResult<T>).FullName}'.");
    }
}
