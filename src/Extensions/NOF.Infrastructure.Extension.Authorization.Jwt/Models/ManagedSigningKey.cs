using Microsoft.IdentityModel.Tokens;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Represents a managed signing key with its metadata.
/// </summary>
public sealed class ManagedSigningKey
{
    /// <summary>
    /// The unique key identifier (kid).
    /// </summary>
    public required string Kid { get; init; }

    /// <summary>
    /// The RSA security key used for signing and validation.
    /// </summary>
    public required RsaSecurityKey Key { get; init; }

    /// <summary>
    /// The UTC time when this key was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }
}
