using Microsoft.IdentityModel.Tokens;

namespace NOF;

/// <summary>
/// Provides signing keys directly from the local <see cref="ISigningKeyService"/>
/// without making HTTP calls. Used by the authority host to validate its own tokens.
/// </summary>
public class LocalJwksProvider : IJwksProvider
{
    private readonly ISigningKeyService _signingKeyService;

    public LocalJwksProvider(ISigningKeyService signingKeyService)
    {
        _signingKeyService = signingKeyService;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = _signingKeyService.AllKeys
            .Select(SecurityKey (k) => k.Key)
            .ToList();

        return Task.FromResult<IReadOnlyList<SecurityKey>>(keys);
    }

    /// <inheritdoc />
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // No-op: local keys are always up-to-date since we hold the key ring directly.
        return Task.CompletedTask;
    }
}
