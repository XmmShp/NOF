using NOF.Abstraction;
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
    public abstract Task<IRpcResult> HandleAsync(object request, NOFContext context, CancellationToken cancellationToken);
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
    public sealed override async Task<IRpcResult> HandleAsync(object request, NOFContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await HandleAsync((TRequest)request, context, cancellationToken).ConfigureAwait(false);
    }

    protected RpcResult<TResponse> Success(TResponse value)
        => RpcResults.Success(value);

    protected RpcResult<TResponse> Success(
        TResponse value,
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => RpcResults.Success(value, statusCode, headers);

    protected RpcResult<TResponse> Success(
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => RpcResults.Success<TResponse>(default!, statusCode, headers);

    protected RpcResult<TResponse> Fail(
        int statusCode,
        object? body = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
        => RpcResults.FromFailure<TResponse>(body, statusCode, headers);

    /// <summary>
    /// Executes the handler using the strongly typed request.
    /// </summary>
    public abstract Task<RpcResult<TResponse>> HandleAsync(TRequest request, NOFContext context, CancellationToken cancellationToken);
}
