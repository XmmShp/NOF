using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Provides access to JSON Web Keys for token validation.
/// Implementations may fetch keys from a remote authority or from a local key service.
/// </summary>
public interface IJwksProvider
{
    /// <summary>
    /// Gets the current set of security keys for token validation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of security keys that can be used to validate tokens.</returns>
    Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the cached keys from the authority.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches JWKS from a remote authority over HTTP and caches the keys.
/// Supports automatic refresh when keys expire or on demand via <see cref="RefreshAsync"/>.
/// </summary>
public class HttpJwksProvider : IJwksProvider
{
    private readonly JwtClientOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private volatile IReadOnlyList<SecurityKey>? _cachedKeys;
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public HttpJwksProvider(IOptions<JwtClientOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedKeys is not null && DateTime.UtcNow - _lastRefreshUtc < _options.CacheLifetime)
        {
            return _cachedKeys;
        }

        await RefreshAsync(cancellationToken);
        return _cachedKeys ?? [];
    }

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var client = _httpClientFactory.CreateClient(JwtClientConstants.JwksHttpClientName);
            var jwksUrl = _options.Authority.TrimEnd('/') + JwtClientConstants.JwksEndpointPath;

            var jwksDocument = await client.GetFromJsonAsync<JwksDocument>(jwksUrl, cancellationToken);
            if (jwksDocument?.Keys is null || jwksDocument.Keys.Length == 0)
            {
                return;
            }

            var keys = new List<SecurityKey>();
            foreach (var jwk in jwksDocument.Keys)
            {
                if (jwk.Kty != "RSA")
                    continue;

                var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = Base64UrlDecode(jwk.N),
                    Exponent = Base64UrlDecode(jwk.E)
                });

                keys.Add(new RsaSecurityKey(rsa) { KeyId = jwk.Kid });
            }

            _cachedKeys = keys;
            _lastRefreshUtc = DateTime.UtcNow;
        }
        finally
        {
            _refreshLock.Release();
        }
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
}
