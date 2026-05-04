namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Service for managing signing keys with rotation support.
/// </summary>
public interface ISigningKeyService
{
    /// <summary>
    /// Gets the current active signing key used for signing new tokens.
    /// </summary>
    Task<ManagedSigningKey> GetCurrentSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active keys (current + retired) that can be used for token validation.
    /// </summary>
    Task<ManagedSigningKey[]> GetAllKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the signing key: generates a new key and retires the current one.
    /// Retired keys are kept for validation up to the configured retention count.
    /// </summary>
    Task RotateKeyAsync(CancellationToken cancellationToken = default);
}
