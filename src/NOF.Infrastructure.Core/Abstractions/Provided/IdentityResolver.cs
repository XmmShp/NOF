using System.Security.Claims;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Resolves user identity from an <see cref="InboundContext"/>.
/// Implementations may use JWT, API keys, cookies, or any other authentication mechanism.
/// </summary>
public interface IIdentityResolver
{
    /// <summary>
    /// Attempts to resolve a <see cref="ClaimsPrincipal"/> from the given inbound context.
    /// Returns <c>null</c> if no valid identity could be resolved.
    /// </summary>
    /// <param name="context">The inbound handler execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ClaimsPrincipal?> ResolveAsync(InboundContext context, CancellationToken cancellationToken = default);
}
