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
    public abstract Task<object?> HandleAsync(object request, CancellationToken cancellationToken);
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
    public sealed override async Task<object?> HandleAsync(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await HandleAsync((TRequest)request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the handler using the strongly typed request.
    /// </summary>
    public abstract Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
