using Microsoft.IdentityModel.Tokens;

namespace NOF.Infrastructure.Extension.Authentication;

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

    /// <summary>
    /// The UTC time when this key most recently became the active signing key.
    /// </summary>
    public required DateTime ActivatedAtUtc { get; init; }
}
