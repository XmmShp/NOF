using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Notification published when signing keys should be rotated.
/// Subscribers should trigger a background refresh of their cached JWKS upon receiving this notification.
/// </summary>
public record JwtKeyRotationNotification : INotification;
