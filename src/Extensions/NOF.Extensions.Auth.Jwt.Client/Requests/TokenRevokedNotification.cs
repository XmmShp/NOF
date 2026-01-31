namespace NOF;

/// <summary>
/// Notification for token revocation.
/// </summary>
public record TokenRevokedNotification(
    string TokenId,
    string? UserId = null,
    string? TenantId = null,
    DateTime RevokedAt = default) : INotification
{
    public DateTime RevokedAt { get; } = RevokedAt == default ? DateTime.UtcNow : RevokedAt;
}
