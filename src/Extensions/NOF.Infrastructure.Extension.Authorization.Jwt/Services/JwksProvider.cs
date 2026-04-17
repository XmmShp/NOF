using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NOF.Contract.Extension.Authorization.Jwt;
using System.Security.Cryptography;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Caches JWKS signing keys locally and refreshes them through <see cref="HttpJwksService"/>.
/// </summary>
public sealed class JwksProvider(IServiceScopeFactory serviceScopeFactory, IServiceProvider serviceProvider) : IJwksProvider, IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly ISigningKeyService? _signingKeyService = serviceProvider.GetService<ISigningKeyService>();
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
            var jwksService = scope.ServiceProvider.GetRequiredService<IJwksServiceClient>();
            var result = await jwksService.GetJwksAsync(new GetJwksRequest());

            if (result.IsSuccess && result.Value?.Keys is { Length: > 0 } jwkKeys)
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
