using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Untyped base class for one split RPC handler.
/// </summary>
public abstract class RpcHandler
{
    /// <summary>
    /// Gets the request type handled by this RPC operation.
    /// </summary>
    public abstract Type RequestType { get; }

    /// <summary>
    /// Gets the response type produced by this RPC operation.
    /// </summary>
    public abstract Type ResponseType { get; }

    /// <summary>
    /// Executes the handler using an untyped request instance.
    /// </summary>
    public abstract Task<IRpcResult> HandleAsync(object request, Context context, CancellationToken cancellationToken);
}

/// <summary>
/// Typed base class for one split RPC handler.
/// </summary>
public abstract class RpcHandler<TRequest, TResponse> : RpcHandler
{
    /// <inheritdoc />
    public override Type RequestType => typeof(TRequest);

    /// <inheritdoc />
    public override Type ResponseType => typeof(TResponse);

    /// <inheritdoc />
    public sealed override async Task<IRpcResult> HandleAsync(object request, Context context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await HandleAsync((TRequest)request, context, cancellationToken).ConfigureAwait(false);
    }

    protected RpcResult<TResponse> Success(TResponse value)
        => RpcResults.Success(value);

    protected RpcResult<TResponse> Success(
        TResponse value,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => RpcResults.Success(value, metadatas);

    protected RpcResult<TResponse> Success(
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => RpcResults.Response<TResponse>(true, null, metadatas);

    protected RpcResult<TResponse> Response(
        object? body = null,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => RpcResults.Response<TResponse>(true, body, metadatas);

    protected RpcResult<TResponse> Fail(
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
        => RpcResults.FromFailure<TResponse>(metadatas);

    protected RpcResult<TResponse> Fail(
        string errorCode,
        string message,
        IDictionary<string, string>? extra = null)
        => RpcResults.BusinessFailure<TResponse>(Result.Fail(errorCode, message, extra));

    /// <summary>
    /// Executes the handler using the strongly typed request.
    /// </summary>
    public abstract Task<RpcResult<TResponse>> HandleAsync(TRequest request, Context context, CancellationToken cancellationToken);
}
