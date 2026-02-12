using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Handles <see cref="JwtKeyRotationNotification"/> by refreshing the cached JWKS from the authority.
/// This ensures clients pick up new signing keys promptly after a key rotation event.
/// </summary>
public class RefreshJwksOnKeyRotation : INotificationHandler<JwtKeyRotationNotification>
{
    private readonly IJwksProvider _jwksProvider;

    public RefreshJwksOnKeyRotation(IJwksProvider jwksProvider)
    {
        _jwksProvider = jwksProvider;
    }

    public async Task HandleAsync(JwtKeyRotationNotification notification, CancellationToken cancellationToken)
    {
        await _jwksProvider.RefreshAsync(cancellationToken);
    }
}
