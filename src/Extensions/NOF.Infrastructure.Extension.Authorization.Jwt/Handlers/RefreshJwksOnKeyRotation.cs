using NOF.Application;
using NOF.Contract.Extension.Authorization.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Refreshes cached JWKS keys when the authority rotates signing keys.
/// </summary>
public sealed class RefreshJwksOnKeyRotation(IJwksProvider jwksProvider) : NotificationHandler<JwtKeyRotationNotification>
{
    public override Task HandleAsync(JwtKeyRotationNotification notification, CancellationToken cancellationToken)
        => jwksProvider.RefreshAsync(cancellationToken);
}
