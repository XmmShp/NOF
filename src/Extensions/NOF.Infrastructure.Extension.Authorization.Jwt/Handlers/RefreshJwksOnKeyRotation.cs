using NOF.Application;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Refreshes cached JWKS keys when the authority rotates signing keys.
/// </summary>
public sealed class RefreshJwksOnKeyRotation(CachedJwksService jwksService) : NotificationHandler<JwtKeyRotationNotification>
{
    public override Task HandleAsync(JwtKeyRotationNotification notification, CancellationToken cancellationToken)
        => jwksService.RefreshAsync(cancellationToken);
}
