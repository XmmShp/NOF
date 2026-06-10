using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Contract.Extension.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static NOF.Infrastructure.Extension.Authentication.OAuthAuthorizationServerServiceHelpers;
using NOF.Abstraction;

namespace NOF.Infrastructure.Extension.Authentication;

public sealed partial class OAuthAuthorizationServerService : RpcServer<IOAuthAuthorizationServerService>;

public sealed class GetRootHandler(IOptions<OAuthAuthorizationServerOptions> options)
    : OAuthAuthorizationServerService.GetRoot
{
    public override Task<Result<OAuthServerRootDocument>> HandleAsync(
        Empty request,
        NOFContext context, CancellationToken cancellationToken)
    {
        var issuer = ResolveIssuer(options.Value);
        return Task.FromResult<Result<OAuthServerRootDocument>>(new OAuthServerRootDocument
        {
            Issuer = issuer,
            Metadata = $"{issuer}/.well-known/oauth-authorization-server"
        });
    }
}

public sealed class GetOpenIdConfigurationHandler(IOptions<OAuthAuthorizationServerOptions> options)
    : OAuthAuthorizationServerService.GetOpenIdConfiguration
{
    public override Task<Result<OAuthServerMetadata>> HandleAsync(
        Empty request,
        NOFContext context, CancellationToken cancellationToken)
        => Task.FromResult(BuildMetadata(options.Value));
}

public sealed class GetAuthorizationServerMetadataHandler(IOptions<OAuthAuthorizationServerOptions> options)
    : OAuthAuthorizationServerService.GetAuthorizationServerMetadata
{
    public override Task<Result<OAuthServerMetadata>> HandleAsync(
        Empty request,
        NOFContext context, CancellationToken cancellationToken)
        => Task.FromResult(BuildMetadata(options.Value));
}

public sealed class GetJwksHandler(IJwksService jwksService)
    : OAuthAuthorizationServerService.GetJwks
{
    public override async Task<Result<JwksDocument>> HandleAsync(
        Empty request,
        NOFContext context, CancellationToken cancellationToken)
        => await jwksService.GetJwksAsync(cancellationToken).ConfigureAwait(false);
}

public sealed class AuthorizeHandler(
    IOAuthAuthorizationHandler authorizationHandler,
    IOAuthAuthorizationCodeService authorizationCodeService)
    : OAuthAuthorizationServerService.Authorize
{
    public override async Task<Result<OAuthAuthorizeResponse>> HandleAsync(
        OAuthAuthorizeRequest request,
        NOFContext context, CancellationToken cancellationToken)
    {
        var authorizationRequest = new OAuthAuthorizationRequest(
            ResponseType: request.ResponseType,
            ClientId: request.ClientId,
            RedirectUri: request.RedirectUri,
            Scope: NormalizeScope(request.Scope),
            State: request.State,
            Nonce: EmptyToNull(request.Nonce),
            CodeChallenge: EmptyToNull(request.CodeChallenge),
            CodeChallengeMethod: EmptyToNull(request.CodeChallengeMethod));

        var validationError = ValidateAuthorizationRequest(authorizationRequest);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await authorizationHandler.AuthorizeAsync(authorizationRequest, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            OAuthAuthorizationResult.Authorized authorized => await RedirectWithCodeAsync(
                authorizationCodeService,
                authorizationRequest,
                authorized.Subject,
                cancellationToken).ConfigureAwait(false),
            OAuthAuthorizationResult.Challenge challenge => Redirect(challenge.RedirectUrl),
            OAuthAuthorizationResult.Failure failure => Error(failure.Error, failure.ErrorDescription),
            _ => Result.Fail("server_error", "Unsupported OAuth authorization result.")
        };
    }

    private static async ValueTask<Result<OAuthAuthorizeResponse>> RedirectWithCodeAsync(
        IOAuthAuthorizationCodeService authorizationCodeService,
        OAuthAuthorizationRequest request,
        string subject,
        CancellationToken cancellationToken)
    {
        var code = await authorizationCodeService.CreateAsync(
            new OAuthAuthorizationCodeDescriptor(
                subject,
                request.ClientId,
                request.RedirectUri,
                request.Scope,
                request.Nonce,
                request.CodeChallenge,
                NormalizeCodeChallengeMethod(request.CodeChallengeMethod)),
            cancellationToken).ConfigureAwait(false);

        return Redirect(AddQueryString(
            request.RedirectUri,
            new Dictionary<string, string?>
            {
                ["code"] = code,
                ["state"] = request.State
            }));
    }

    private static Result<OAuthAuthorizeResponse>? ValidateAuthorizationRequest(OAuthAuthorizationRequest request)
    {
        if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
        {
            return Error("unsupported_response_type", "Only response_type=code is supported.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return Error("invalid_request", "client_id is required.");
        }

        if (!Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
        {
            return Error("invalid_request", "redirect_uri must be an absolute URI.");
        }

        var pkceValidation = ValidateCodeChallenge(request.CodeChallenge, request.CodeChallengeMethod);
        return pkceValidation is null ? null : Error("invalid_request", pkceValidation);
    }

    private static Result<OAuthAuthorizeResponse> Redirect(string redirectUrl)
        => new OAuthAuthorizeResponse
        {
            Type = OAuthAuthorizeResponseType.Redirect,
            RedirectUrl = redirectUrl
        };

    private static Result<OAuthAuthorizeResponse> Error(string error, string description)
        => new OAuthAuthorizeResponse
        {
            Type = OAuthAuthorizeResponseType.Error,
            Error = new OAuthError
            {
                Error = error,
                ErrorDescription = description
            }
        };
}

public sealed class TokenHandler(
    ICacheService cacheService,
    IOAuthSubjectService subjectService,
    ITokenService tokenService,
    ISigningKeyService signingKeyService,
    IOptions<AuthenticationAuthorityOptions> authorityOptions,
    IOptions<OAuthAuthorizationServerOptions> oauthOptions)
    : OAuthAuthorizationServerService.Token
{
    public override async Task<Result<OAuthTokenEndpointResponse>> HandleAsync(
        OAuthTokenRequest request,
        NOFContext context, CancellationToken cancellationToken)
    {
        return request.GrantType switch
        {
            "authorization_code" => await TokenFromAuthorizationCodeAsync(request, cancellationToken).ConfigureAwait(false),
            "refresh_token" => await TokenFromRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false),
            _ => Result.Fail("unsupported_grant_type", "Only authorization_code and refresh_token are supported.")
        };
    }

    private async Task<Result<OAuthTokenEndpointResponse>> TokenFromAuthorizationCodeAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result.Fail("invalid_request", "code is required.");
        }

        var cachedCode = await cacheService
            .GetAndRemoveAsync(new OAuthAuthorizationCodeCacheKey(request.Code), cancellationToken)
            .ConfigureAwait(false);
        var authorizationCode = cachedCode.HasValue ? cachedCode.Value : null;
        if (authorizationCode is null)
        {
            var redeemed = await cacheService
                .GetAsync(new OAuthRedeemedAuthorizationCodeCacheKey(request.Code), cancellationToken)
                .ConfigureAwait(false);
            if (!redeemed.HasValue
                || !FixedTimeEquals(redeemed.Value.ClientId, request.ClientId)
                || !FixedTimeEquals(redeemed.Value.RedirectUri, request.RedirectUri))
            {
                return Result.Fail("invalid_grant", "authorization code is invalid or expired.");
            }

            return redeemed.Value.Response;
        }

        if (authorizationCode.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Result.Fail("invalid_grant", "authorization code is expired.");
        }

        if (!FixedTimeEquals(authorizationCode.ClientId, request.ClientId)
            || !FixedTimeEquals(authorizationCode.RedirectUri, request.RedirectUri))
        {
            return Result.Fail("invalid_grant", "authorization code client or redirect_uri does not match.");
        }

        var verifierError = ValidateCodeVerifier(authorizationCode, request.CodeVerifier);
        if (verifierError is not null)
        {
            return Result.Fail("invalid_grant", verifierError);
        }

        var response = await IssueTokenResponseAsync(
            authorizationCode.Subject,
            authorizationCode.Scope,
            authorizationCode.ClientId,
            authorizationCode.Nonce,
            cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Result.Fail("invalid_grant", "authorization code subject is invalid.");
        }

        await cacheService.SetAsync(
            new OAuthRedeemedAuthorizationCodeCacheKey(request.Code),
            new OAuthRedeemedAuthorizationCodeCacheValue
            {
                ClientId = authorizationCode.ClientId,
                RedirectUri = authorizationCode.RedirectUri,
                Response = response
            },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = oauthOptions.Value.RedeemedAuthorizationCodeGracePeriod
            },
            cancellationToken).ConfigureAwait(false);

        return response;
    }

    private async Task<Result<OAuthTokenEndpointResponse>> TokenFromRefreshTokenAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Fail("invalid_request", "refresh_token is required.");
        }

        var validateResult = await tokenService.ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest { RefreshToken = request.RefreshToken },
            cancellationToken).ConfigureAwait(false);
        if (!validateResult.IsSuccess)
        {
            return Result.Fail("invalid_grant", validateResult.Message);
        }

        var claims = validateResult.Value.Claims
            .GroupBy(static claim => claim.Type, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().Value, StringComparer.Ordinal);
        if (!claims.TryGetValue(OAuthClaimTypes.Subject, out var subject) || string.IsNullOrWhiteSpace(subject))
        {
            return Result.Fail("invalid_grant", "refresh token subject is invalid.");
        }

        var scope = claims.TryGetValue(OAuthClaimTypes.Scope, out var scopeClaim) ? scopeClaim : string.Empty;
        var scopes = ParseScopes(scope);
        if (!await subjectService.CanRefreshAsync(subject, validateResult.Value.TokenId, scopes, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail("invalid_grant", "refresh token session is invalid.");
        }

        var revokeResult = await tokenService.RevokeRefreshTokenAsync(
            new RevokeRefreshTokenRequest
            {
                TokenId = validateResult.Value.TokenId,
                Expiration = oauthOptions.Value.RefreshTokenExpiration
            },
            cancellationToken).ConfigureAwait(false);
        if (!revokeResult.IsSuccess)
        {
            return Result.Fail("invalid_grant", revokeResult.Message);
        }

        var response = await IssueTokenResponseAsync(
            subject,
            scope,
            idTokenAudience: null,
            nonce: null,
            cancellationToken).ConfigureAwait(false);

        return response is null
            ? Result.Fail("invalid_grant", "refresh token subject is invalid.")
            : response;
    }

    private async ValueTask<OAuthTokenEndpointResponse?> IssueTokenResponseAsync(
        string subject,
        string scope,
        string? idTokenAudience,
        string? nonce,
        CancellationToken cancellationToken)
    {
        var scopes = ParseScopes(scope);
        var profile = await subjectService.GetProfileAsync(subject, scopes, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        var accessClaims = profile.AccessTokenClaims
            .Select(static claim => new TokenClaim(claim.Key, claim.Value))
            .ToList();
        accessClaims.Add(new TokenClaim(OAuthClaimTypes.Scope, string.Join(' ', scopes)));

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = oauthOptions.Value.AccessTokenAudience,
                AccessTokenExpiration = oauthOptions.Value.AccessTokenExpiration,
                AccessClaims = accessClaims.ToArray(),
                RefreshToken = new RefreshTokenOptions
                {
                    Expiration = oauthOptions.Value.RefreshTokenExpiration,
                    Claims =
                    [
                        new TokenClaim(OAuthClaimTypes.Subject, subject),
                        new TokenClaim(OAuthClaimTypes.Scope, string.Join(' ', scopes))
                    ]
                }
            },
            cancellationToken).ConfigureAwait(false);
        if (!generateResult.IsSuccess || generateResult.Value.RefreshToken is null)
        {
            return null;
        }

        var idToken = await GenerateIdTokenAsync(
            profile,
            scopes,
            idTokenAudience,
            nonce,
            cancellationToken).ConfigureAwait(false);

        return new OAuthTokenEndpointResponse
        {
            AccessToken = generateResult.Value.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = (long)oauthOptions.Value.AccessTokenExpiration.TotalSeconds,
            RefreshToken = generateResult.Value.RefreshToken.Token,
            Scope = string.Join(' ', scopes),
            IdToken = idToken
        };
    }

    private async ValueTask<string?> GenerateIdTokenAsync(
        OAuthSubjectProfile profile,
        IReadOnlySet<string> scopes,
        string? audience,
        string? nonce,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(audience) || !scopes.Contains(OAuthScope.OpenId))
        {
            return null;
        }

        var claims = profile.IdentityClaims
            .Where(claim => ShouldEmitIdentityClaim(claim.Key, scopes))
            .Select(claim => claim.Key == OAuthClaimTypes.IssuedAt && long.TryParse(claim.Value, out _)
                ? new Claim(claim.Key, claim.Value, ClaimValueTypes.Integer64)
                : new Claim(claim.Key, claim.Value))
            .ToList();
        if (claims.All(static claim => claim.Type != OAuthClaimTypes.Subject))
        {
            claims.Insert(0, new Claim(OAuthClaimTypes.Subject, profile.Subject));
        }

        claims.Add(new Claim(
            OAuthClaimTypes.IssuedAt,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));

        if (!string.IsNullOrWhiteSpace(nonce))
        {
            claims.Add(new Claim(OAuthClaimTypes.Nonce, nonce));
        }

        var now = DateTime.UtcNow;
        var signingKey = (await signingKeyService.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false)).Key;
        var jwtToken = new JwtSecurityToken(
            issuer: authorityOptions.Value.Issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(oauthOptions.Value.AccessTokenExpiration),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }
}

public sealed class UserInfoHandler(
    IOAuthSubjectService subjectService,
    ISigningKeyService signingKeyService,
    IOptions<AuthenticationAuthorityOptions> authorityOptions)
    : OAuthAuthorizationServerService.UserInfo
{
    public override async Task<Result<IReadOnlyDictionary<string, object>>> HandleAsync(
        OAuthUserInfoRequest request,
        NOFContext context, CancellationToken cancellationToken)
    {
        var principal = await ValidateAccessTokenAsync(
            request.AccessToken.Value,
            signingKeyService,
            authorityOptions.Value,
            cancellationToken).ConfigureAwait(false);
        var subject = principal?.FindFirst(OAuthClaimTypes.Subject)?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Fail("401", "access token is invalid.");
        }

        var scope = principal!.FindFirst(OAuthClaimTypes.Scope)?.Value ?? string.Empty;
        var scopes = ParseScopes(scope);
        var profile = await subjectService.GetProfileAsync(subject, scopes, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Fail("401", "access token subject is invalid.");
        }

        return profile.IdentityClaims
            .Where(claim => ShouldEmitIdentityClaim(claim.Key, scopes))
            .GroupBy(static claim => claim.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Count() == 1
                    ? (object)group.First().Value
                    : group.Select(static claim => claim.Value).ToArray(),
                StringComparer.Ordinal);
    }
}

internal static class OAuthAuthorizationServerServiceHelpers
{
    public static Result<OAuthServerMetadata> BuildMetadata(OAuthAuthorizationServerOptions options)
    {
        var issuer = ResolveIssuer(options);
        return new OAuthServerMetadata
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/authorize",
            TokenEndpoint = $"{issuer}/token",
            UserInfoEndpoint = $"{issuer}/userinfo",
            JwksUri = $"{issuer}/.well-known/jwks.json",
            ResponseTypesSupported = ["code"],
            GrantTypesSupported = ["authorization_code", "refresh_token"],
            TokenEndpointAuthMethodsSupported = ["client_secret_basic", "client_secret_post", "none"],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = [SecurityAlgorithms.RsaSha256],
            CodeChallengeMethodsSupported = ["plain", "S256"],
            ScopesSupported = options.ScopesSupported,
            ClaimsSupported = options.ClaimsSupported
        };
    }

    public static string ResolveIssuer(OAuthAuthorizationServerOptions options)
        => options.Issuer.TrimEnd('/');

    public static string? ValidateCodeChallenge(string? codeChallenge, string? codeChallengeMethod)
    {
        if (string.IsNullOrWhiteSpace(codeChallenge))
        {
            return null;
        }

        if (codeChallenge.Length is < 43 or > 128)
        {
            return "code_challenge length is invalid.";
        }

        var normalizedMethod = NormalizeCodeChallengeMethod(codeChallengeMethod);
        return string.Equals(normalizedMethod, "plain", StringComparison.Ordinal)
            || string.Equals(normalizedMethod, "S256", StringComparison.Ordinal)
            ? null
            : "code_challenge_method is not supported.";
    }

    public static string? ValidateCodeVerifier(OAuthAuthorizationCodeCacheValue code, string codeVerifier)
    {
        if (string.IsNullOrWhiteSpace(code.CodeChallenge))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(codeVerifier))
        {
            return "code_verifier is required.";
        }

        var expectedChallenge = string.Equals(code.CodeChallengeMethod, "S256", StringComparison.Ordinal)
            ? Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
            : codeVerifier;

        return FixedTimeEquals(code.CodeChallenge, expectedChallenge)
            ? null
            : "code_verifier is invalid.";
    }

    public static IReadOnlySet<string> ParseScopes(string scope)
        => scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    public static string NormalizeScope(string scope)
        => string.Join(' ', ParseScopes(scope).OrderBy(static value => value, StringComparer.Ordinal));

    public static string NormalizeCodeChallengeMethod(string? codeChallengeMethod)
        => string.IsNullOrWhiteSpace(codeChallengeMethod) ? "plain" : codeChallengeMethod.Trim();

    public static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public static bool ShouldEmitIdentityClaim(string claimType, IReadOnlySet<string> scopes)
    {
        return claimType switch
        {
            OAuthClaimTypes.Email => scopes.Contains(OAuthScope.Email),
            OAuthClaimTypes.Name or OAuthClaimTypes.Groups => scopes.Contains(OAuthScope.Profile),
            OAuthClaimTypes.Scope or OAuthClaimTypes.SessionId => false,
            _ => true
        };
    }

    public static async ValueTask<ClaimsPrincipal?> ValidateAccessTokenAsync(
        string token,
        ISigningKeyService signingKeyService,
        AuthenticationAuthorityOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(
                token.Trim(),
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = (await signingKeyService.GetAllKeysAsync(cancellationToken).ConfigureAwait(false)).Select(static key => key.Key),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(options.Issuer),
                    ValidIssuer = options.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                },
                out _);
        }
        catch
        {
            return null;
        }
    }

    public static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    public static string AddQueryString(string uri, IReadOnlyDictionary<string, string?> query)
    {
        var separator = uri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var builder = new StringBuilder(uri);
        foreach (var (key, value) in query)
        {
            if (value is null)
            {
                continue;
            }

            builder.Append(separator);
            separator = '&';
            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
        }

        return builder.ToString();
    }
}
