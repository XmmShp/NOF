namespace NOF;

/// <summary>
/// Notification published when signing keys should be rotated.
/// Subscribers should trigger a background refresh of their cached JWKS upon receiving this notification.
/// </summary>
public record KeyRotationNotification : INotification;
