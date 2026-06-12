using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

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
public abstract class RpcHandler<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TResponse> : RpcHandler
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

    protected TResponse Fail(
        string errorCode,
        string message,
        IDictionary<string, string>? extra = null)
        => (TResponse)ResultProjection.CreateFailure(typeof(TResponse), Result.Fail(errorCode, message, extra));

    /// <summary>
    /// Executes the handler using the strongly typed request.
    /// </summary>
    public abstract Task<TResponse> HandleAsync(TRequest request, Context context, CancellationToken cancellationToken);
}
