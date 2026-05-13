using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Determines whether an inbound RPC request is allowed to execute.
/// </summary>
public interface IRequestAuthorizationPolicy
{
    /// <summary>
    /// Returns <see langword="null" /> when the request is authorized by this policy; otherwise returns the response
    /// that should be used if no registered policy authorizes the request.
    /// </summary>
    ValueTask<IResult?> AuthorizeAsync(RequestInboundContext context, CancellationToken cancellationToken);
}
