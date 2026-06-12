using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using System.Security.Cryptography;

namespace NOF.Infrastructure.Extension.Authentication;

public sealed class OAuthAuthorizationCodeService(
    ICacheService cacheService,
    IOptions<OAuthAuthorizationServerOptions> options) : IOAuthAuthorizationCodeService
{
    public async ValueTask<string> CreateAsync(
        OAuthAuthorizationCodeDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.RedirectUri);

        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = DateTime.UtcNow.Add(options.Value.AuthorizationCodeExpiration);
        await cacheService.SetAsync(
            new OAuthAuthorizationCodeCacheKey(code),
            new OAuthAuthorizationCodeCacheValue
            {
                Subject = descriptor.Subject,
                ClientId = descriptor.ClientId,
                RedirectUri = descriptor.RedirectUri,
                Scope = descriptor.Scope,
                Nonce = descriptor.Nonce,
                CodeChallenge = descriptor.CodeChallenge,
                CodeChallengeMethod = descriptor.CodeChallengeMethod,
                ExpiresAtUtc = expiresAtUtc
            },
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAtUtc },
            cancellationToken);

        return code;
    }
}
