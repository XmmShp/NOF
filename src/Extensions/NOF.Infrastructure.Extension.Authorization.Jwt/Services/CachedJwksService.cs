using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Maintains a local cache of JWKS keys and refreshes them from the configured authority when needed.
/// </summary>
public sealed class CachedJwksService(IServiceScopeFactory serviceScopeFactory, ISigningKeyService? signingKeyService) : IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly ISigningKeyService? _signingKeyService = signingKeyService;
    private IReadOnlyList<SecurityKey>? _cachedKeys;

    public async Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedKeys is { Count: > 0 } cachedKeys)
        {
            return cachedKeys;
        }

        return await RefreshAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SecurityKey>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_signingKeyService is not null)
            {
                _cachedKeys = ToSecurityKeys(_signingKeyService.AllKeys);
                return _cachedKeys;
            }

            using var scope = serviceScopeFactory.CreateScope();
            var jwksService = scope.ServiceProvider.GetRequiredService<IJwksService>();
            var document = await jwksService.GetJwksAsync(cancellationToken);

            if (document.Keys is { Length: > 0 } jwkKeys)
            {
                _cachedKeys = ToSecurityKeys(jwkKeys);
            }

            return _cachedKeys ?? [];
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static IReadOnlyList<SecurityKey> ToSecurityKeys(IReadOnlyCollection<ManagedSigningKey> managedKeys)
    {
        return managedKeys
            .Select(managedKey => (SecurityKey)new RsaSecurityKey(managedKey.Key.Rsa) { KeyId = managedKey.Kid })
            .ToArray();
    }

    private static IReadOnlyList<SecurityKey> ToSecurityKeys(JsonWebKey[] jwkKeys)
    {
        var keys = new List<SecurityKey>();

        foreach (var jwk in jwkKeys)
        {
            if (jwk.Kty != "RSA")
            {
                continue;
            }

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(jwk.N),
                Exponent = Base64UrlDecode(jwk.E)
            });

            keys.Add(new RsaSecurityKey(rsa) { KeyId = jwk.Kid });
        }

        return keys;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2:
                output += "==";
                break;
            case 3:
                output += "=";
                break;
        }

        return Convert.FromBase64String(output);
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }
}
