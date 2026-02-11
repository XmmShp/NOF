namespace NOF;

/// <summary>
/// Handles <see cref="KeyRotationNotification"/> by refreshing the cached JWKS from the authority.
/// This ensures clients pick up new signing keys promptly after a key rotation event.
/// </summary>
public class RefreshJwksOnKeyRotation : INotificationHandler<KeyRotationNotification>
{
    private readonly IJwksProvider _jwksProvider;

    public RefreshJwksOnKeyRotation(IJwksProvider jwksProvider)
    {
        _jwksProvider = jwksProvider;
    }

    public async Task HandleAsync(KeyRotationNotification notification, CancellationToken cancellationToken)
    {
        await _jwksProvider.RefreshAsync(cancellationToken);
    }
}
