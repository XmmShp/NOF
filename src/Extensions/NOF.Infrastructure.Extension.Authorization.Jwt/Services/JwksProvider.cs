using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

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
    private readonly JwtAuthorizationOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private volatile IReadOnlyList<SecurityKey>? _cachedKeys;
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public HttpJwksProvider(IOptions<JwtAuthorizationOptions> options, IHttpClientFactory httpClientFactory)
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
            var client = _httpClientFactory.CreateClient(NOFJwtAuthorizationConstants.JwtClient.JwksHttpClientName);
            var jwksUrl = _options.JwksEndpoint;

            var jwksDocument = await client.GetFromJsonAsync(jwksUrl, JwksJsonContext.Default.JwksHttpDocument, cancellationToken);
            if (jwksDocument?.Keys is null || jwksDocument.Keys.Length == 0)
            {
                return;
            }

            var keys = new List<SecurityKey>();
            foreach (var jwk in jwksDocument.Keys)
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

/// <summary>
/// Fetches JWKS via in-process request handler execution and caches the keys.
/// The provider creates a scope on each refresh and executes the handler through
/// NOF inbound/outbound pipelines.
/// </summary>
public class RequestDispatcherJwksProvider : IJwksProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly JwtAuthorizationOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private volatile IReadOnlyList<SecurityKey>? _cachedKeys;
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public RequestDispatcherJwksProvider(IServiceProvider serviceProvider, IOptions<JwtAuthorizationOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
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
            await using var scope = _serviceProvider.CreateAsyncScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IRequestHandlerResolver>();
            var resolved = resolver.ResolveRequestWithResponse(typeof(GetJwksRequest))
                ?? throw new InvalidOperationException("No local handler registered for GetJwksRequest.");

            var handler = (IRequestHandler)scope.ServiceProvider.GetRequiredKeyedService(resolved.HandlerType, resolved.Key);
            var inboundPipeline = scope.ServiceProvider.GetRequiredService<IInboundPipelineExecutor>();
            var outboundPipeline = scope.ServiceProvider.GetRequiredService<IOutboundPipelineExecutor>();

            var request = new GetJwksRequest();
            var outboundContext = new OutboundContext
            {
                Message = request,
                Headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            };

            await outboundPipeline.ExecuteAsync(outboundContext, async ct =>
            {
                var inboundContext = new InboundContext
                {
                    Message = request,
                    Handler = handler,
                    Headers = outboundContext.Headers
                };

                await inboundPipeline.ExecuteAsync(inboundContext, async innerCt =>
                {
                    inboundContext.Response = await handler.HandleAsync(request, innerCt).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);

                outboundContext.Response = inboundContext.Response;
            }, cancellationToken).ConfigureAwait(false);

            var result = Result.From<GetJwksResponse>(outboundContext.Response!);
            if (!result.IsSuccess || result.Value.Jwks.Keys is not { Length: > 0 } jwkKeys)
            {
                return;
            }

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
