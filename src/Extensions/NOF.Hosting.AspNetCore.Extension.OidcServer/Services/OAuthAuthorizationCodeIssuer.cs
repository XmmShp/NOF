using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using System.Security.Cryptography;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OAuthAuthorizationCodeIssuer(
    ICacheService cacheService,
    IOptions<OAuthAuthorizationServerOptions> options)
{
    public async ValueTask<string> CreateRedirectUriAsync(
        OAuthAuthorizationRequest request,
        string subject,
        bool wasRedirectUriSupplied,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RedirectUri);

        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = DateTime.UtcNow.Add(options.Value.AuthorizationCodeExpiration);
        await cacheService.SetAsync(
            new OidcAuthorizationCodeCacheKey(code),
            new OidcAuthorizationCodeCacheValue
            {
                Subject = subject,
                ClientId = request.ClientId,
                RedirectUri = request.RedirectUri,
                WasRedirectUriSupplied = wasRedirectUriSupplied,
                Scope = request.Scope,
                Nonce = request.Nonce,
                CodeChallenge = request.CodeChallenge,
                CodeChallengeMethod = string.IsNullOrWhiteSpace(request.CodeChallengeMethod)
                    ? "plain"
                    : request.CodeChallengeMethod.Trim(),
                ExpiresAtUtc = expiresAtUtc
            },
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAtUtc },
            cancellationToken).ConfigureAwait(false);

        return AddQueryString(
            request.RedirectUri,
            new Dictionary<string, string?>
            {
                ["code"] = code,
                ["state"] = request.State
            });
    }

    private static string AddQueryString(string uri, IReadOnlyDictionary<string, string?> query)
    {
        var builder = new UriBuilder(uri);
        var values = new List<string>();
        if (!string.IsNullOrEmpty(builder.Query))
        {
            values.Add(builder.Query.TrimStart('?'));
        }

        values.AddRange(query
            .Where(static item => !string.IsNullOrEmpty(item.Value))
            .Select(static item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}"));
        builder.Query = string.Join('&', values);
        return builder.Uri.ToString();
    }
}

internal sealed record OidcAuthorizationCodeCacheKey(string Code)
    : CacheKey<OidcAuthorizationCodeCacheValue>($"nof:oauth:auth-code:{Code}");

internal sealed record OidcAuthorizationCodeCacheValue
{
    public required string Subject { get; init; }

    public required string ClientId { get; init; }

    public required string RedirectUri { get; init; }

    public required bool WasRedirectUriSupplied { get; init; }

    public required string Scope { get; init; }

    public string? Nonce { get; init; }

    public string? CodeChallenge { get; init; }

    public string? CodeChallengeMethod { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }
}
