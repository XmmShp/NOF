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
    public abstract Task<IResult> HandleAsync(object request, Context context, CancellationToken cancellationToken);
}

/// <summary>
/// Typed base class for one split RPC handler.
/// </summary>
public abstract class RpcHandler<TRequest, TResponse> : RpcHandler
    where TResponse : IResult
{
    /// <inheritdoc />
    public override Type RequestType => typeof(TRequest);

    /// <inheritdoc />
    public override Type ResponseType => typeof(TResponse);

    /// <inheritdoc />
    public sealed override async Task<IResult> HandleAsync(object request, Context context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await HandleAsync((TRequest)request, context, cancellationToken).ConfigureAwait(false);
    }

    protected TResponse Success(TResponse value)
        => value;

    protected TResponse Success(
        Context context,
        TResponse value,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
    {
        ApplyResponseMetadatas(context, metadatas);
        return value;
    }

    protected TResponse Success(
        Context context,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
    {
        ApplyResponseMetadatas(context, metadatas);
        return (TResponse)ResultProjection.CreateSuccess(typeof(TResponse));
    }

    protected TResponse Response(
        Context context,
        TResponse body,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
    {
        ApplyResponseMetadatas(context, metadatas);
        return body;
    }

    protected TResponse Fail(
        Context context,
        IEnumerable<KeyValuePair<string, string?>>? metadatas = null)
    {
        ApplyResponseMetadatas(context, metadatas);
        return (TResponse)ResultProjection.CreateFailure(typeof(TResponse), Result.Fail("500", "RPC call failed."));
    }

    protected TResponse Fail(
        string errorCode,
        string message,
        IDictionary<string, string>? extra = null)
        => (TResponse)ResultProjection.CreateFailure(typeof(TResponse), Result.Fail(errorCode, message, extra));

    private static void ApplyResponseMetadatas(Context context, IEnumerable<KeyValuePair<string, string?>>? metadatas)
        => context.SetResponseMetadatas(metadatas);

    /// <summary>
    /// Executes the handler using the strongly typed request.
    /// </summary>
    public abstract Task<TResponse> HandleAsync(TRequest request, Context context, CancellationToken cancellationToken);
}
