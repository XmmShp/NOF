using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AspNetResult = Microsoft.AspNetCore.Http.IResult;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public static partial class NOFOidcServerExtensions
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapOidcServer(string? prefix = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            var options = app.ServiceProvider.GetRequiredService<IOptions<OAuthAuthorizationServerOptions>>().Value;
            var pathBase = NormalizePathBase(options.PathBase);
            var group = app.MapGroup(CombineRoute(prefix, pathBase));

            group.MapGet(string.Empty, static (IOptions<OAuthAuthorizationServerOptions> oidcOptions) =>
            {
                var issuer = ResolveIssuer(oidcOptions.Value);
                return Results.Json(new OAuthServerRootDocument
                {
                    Issuer = issuer,
                    Metadata = OAuthAuthorizationServerMetadataUris.BuildMetadataEndpoint(issuer, requireHttps: false).ToString()
                });
            });

            MapMetadataEndpoints(app, prefix, pathBase);

            group.MapGet("/authorize", AuthorizeAsync);
            group.MapPost("/token", TokenAsync);
            group.MapGet("/userinfo", UserInfoAsync);
            group.MapPost("/userinfo", UserInfoAsync);

            return app;
        }
    }

    private static void MapMetadataEndpoints(IEndpointRouteBuilder app, string? prefix, string pathBase)
    {
        app.MapGet(CombineRoute(prefix, "/.well-known/openid-configuration"),
            static (IOptions<OAuthAuthorizationServerOptions> oidcOptions) => Results.Json(BuildMetadata(oidcOptions.Value)));
        app.MapGet(CombineRoute(prefix, $"{pathBase}/.well-known/openid-configuration"),
            static (IOptions<OAuthAuthorizationServerOptions> oidcOptions) => Results.Json(BuildMetadata(oidcOptions.Value)));

        app.MapGet(CombineRoute(prefix, "/.well-known/oauth-authorization-server"),
            static (IOptions<OAuthAuthorizationServerOptions> oidcOptions) => Results.Json(BuildMetadata(oidcOptions.Value)));
        app.MapGet(CombineRoute(prefix, BuildOAuthAuthorizationServerMetadataRoute(pathBase)),
            static (IOptions<OAuthAuthorizationServerOptions> oidcOptions) => Results.Json(BuildMetadata(oidcOptions.Value)));

        app.MapGet(CombineRoute(prefix, "/.well-known/jwks.json"),
            static async (IJwksService jwksService, CancellationToken cancellationToken)
                => Results.Json(await jwksService.GetJwksAsync(cancellationToken).ConfigureAwait(false)));
        app.MapGet(CombineRoute(prefix, $"{pathBase}/.well-known/jwks.json"),
            static async (IJwksService jwksService, CancellationToken cancellationToken)
                => Results.Json(await jwksService.GetJwksAsync(cancellationToken).ConfigureAwait(false)));
    }

    private static async Task<AspNetResult> AuthorizeAsync(
        [AsParameters] OAuthAuthorizeRequest request,
        IOAuthAuthorizationHandler authorizationHandler,
        IOAuthAuthorizationCodeService authorizationCodeService,
        CancellationToken cancellationToken)
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
            return CreateAuthorizeFailureResult(authorizationRequest, validationError);
        }

        var result = await authorizationHandler.AuthorizeAsync(authorizationRequest, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            OAuthAuthorizationResult.Authorized authorized => Results.Redirect(
                await RedirectWithCodeAsync(authorizationCodeService, authorizationRequest, authorized.Subject, cancellationToken).ConfigureAwait(false)),
            OAuthAuthorizationResult.Challenge challenge => Results.Redirect(challenge.RedirectUrl),
            OAuthAuthorizationResult.Failure failure => CreateAuthorizeFailureResult(
                authorizationRequest,
                CreateOAuthError(failure.Error, failure.ErrorDescription)),
            _ => Results.Json(
                CreateOAuthError("server_error", "Unsupported OAuth authorization result."),
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<AspNetResult> TokenAsync(
        [FromForm] OAuthTokenRequest request,
        ICacheService cacheService,
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        IOptions<AuthenticationAuthorityOptions> authorityOptions,
        IOptions<OAuthAuthorizationServerOptions> oauthOptions,
        CancellationToken cancellationToken)
    {
        var result = request.GrantType switch
        {
            "authorization_code" => await TokenFromAuthorizationCodeAsync(
                request,
                cacheService,
                subjectService,
                tokenService,
                signingKeyService,
                authorityOptions.Value,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            "refresh_token" => await TokenFromRefreshTokenAsync(
                request,
                subjectService,
                tokenService,
                signingKeyService,
                authorityOptions.Value,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            _ => Fail("unsupported_grant_type", "Only authorization_code and refresh_token are supported.")
        };

        return result.IsSuccess
            ? Results.Json(result.Value)
            : CreateOAuthErrorResult(result.ErrorCode, result.Message);
    }

    private static async Task<AspNetResult> UserInfoAsync(
        HttpRequest httpRequest,
        IOAuthSubjectService subjectService,
        ISigningKeyService signingKeyService,
        IOptions<AuthenticationAuthorityOptions> authorityOptions,
        CancellationToken cancellationToken)
    {
        var accessToken = await ResolveBearerTokenAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var principal = await ValidateAccessTokenAsync(
            accessToken,
            signingKeyService,
            authorityOptions.Value,
            cancellationToken).ConfigureAwait(false);
        var subject = principal?.FindFirst(OAuthClaimTypes.Subject)?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return CreateOAuthErrorResult("invalid_token", "access token is invalid.", StatusCodes.Status401Unauthorized);
        }

        var scope = principal!.FindFirst(OAuthClaimTypes.Scope)?.Value ?? string.Empty;
        var scopes = ParseScopes(scope);
        var profile = await subjectService.GetProfileAsync(subject, scopes, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return CreateOAuthErrorResult("invalid_token", "access token subject is invalid.", StatusCodes.Status401Unauthorized);
        }

        IReadOnlyDictionary<string, object> claims = profile.IdentityClaims
            .Where(claim => ShouldEmitIdentityClaim(claim.Key, scopes))
            .GroupBy(static claim => claim.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Count() == 1
                    ? (object)group.First().Value
                    : group.Select(static claim => claim.Value).ToArray(),
                StringComparer.Ordinal);

        return Results.Json(claims);
    }

    private static async Task<Result<OAuthTokenEndpointResponse>> TokenFromAuthorizationCodeAsync(
        OAuthTokenRequest request,
        ICacheService cacheService,
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        AuthenticationAuthorityOptions authorityOptions,
        OAuthAuthorizationServerOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Fail("invalid_request", "code is required.");
        }

        var cachedCode = await cacheService
            .GetAndRemoveAsync(new OidcAuthorizationCodeCacheKey(request.Code), cancellationToken)
            .ConfigureAwait(false);
        var authorizationCode = cachedCode.HasValue ? cachedCode.Value : null;
        if (authorizationCode is null)
        {
            var redeemed = await cacheService
                .GetAsync(new OidcRedeemedAuthorizationCodeCacheKey(request.Code), cancellationToken)
                .ConfigureAwait(false);
            if (!redeemed.HasValue
                || !FixedTimeEquals(redeemed.Value.ClientId, request.ClientId)
                || !FixedTimeEquals(redeemed.Value.RedirectUri, request.RedirectUri))
            {
                return Fail("invalid_grant", "authorization code is invalid or expired.");
            }

            return Result.Success(redeemed.Value.Response);
        }

        if (authorizationCode.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Fail("invalid_grant", "authorization code is expired.");
        }

        if (!FixedTimeEquals(authorizationCode.ClientId, request.ClientId)
            || !FixedTimeEquals(authorizationCode.RedirectUri, request.RedirectUri))
        {
            return Fail("invalid_grant", "authorization code client or redirect_uri does not match.");
        }

        var verifierError = ValidateCodeVerifier(authorizationCode, request.CodeVerifier);
        if (verifierError is not null)
        {
            return Fail("invalid_grant", verifierError);
        }

        var response = await IssueTokenResponseAsync(
            subjectService,
            tokenService,
            signingKeyService,
            authorityOptions,
            oauthOptions,
            authorizationCode.Subject,
            authorizationCode.Scope,
            authorizationCode.ClientId,
            authorizationCode.Nonce,
            cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Fail("invalid_grant", "authorization code subject is invalid.");
        }

        await cacheService.SetAsync(
            new OidcRedeemedAuthorizationCodeCacheKey(request.Code),
            new OidcRedeemedAuthorizationCodeCacheValue
            {
                ClientId = authorizationCode.ClientId,
                RedirectUri = authorizationCode.RedirectUri,
                Response = response
            },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = oauthOptions.RedeemedAuthorizationCodeGracePeriod
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(response);
    }

    private static async Task<Result<OAuthTokenEndpointResponse>> TokenFromRefreshTokenAsync(
        OAuthTokenRequest request,
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        AuthenticationAuthorityOptions authorityOptions,
        OAuthAuthorizationServerOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Fail("invalid_request", "refresh_token is required.");
        }

        var validateResult = await tokenService.ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest { RefreshToken = request.RefreshToken },
            cancellationToken).ConfigureAwait(false);
        if (!validateResult.IsSuccess)
        {
            return Fail("invalid_grant", validateResult.Message);
        }

        var claims = validateResult.Value.Claims
            .GroupBy(static claim => claim.Type, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().Value, StringComparer.Ordinal);
        if (!claims.TryGetValue(OAuthClaimTypes.Subject, out var subject) || string.IsNullOrWhiteSpace(subject))
        {
            return Fail("invalid_grant", "refresh token subject is invalid.");
        }

        var scope = claims.TryGetValue(OAuthClaimTypes.Scope, out var scopeClaim) ? scopeClaim : string.Empty;
        var scopes = ParseScopes(scope);
        if (!await subjectService.CanRefreshAsync(subject, validateResult.Value.TokenId, scopes, cancellationToken).ConfigureAwait(false))
        {
            return Fail("invalid_grant", "refresh token session is invalid.");
        }

        var revokeResult = await tokenService.RevokeRefreshTokenAsync(
            new RevokeRefreshTokenRequest
            {
                TokenId = validateResult.Value.TokenId,
                Expiration = oauthOptions.RefreshTokenExpiration
            },
            cancellationToken).ConfigureAwait(false);
        if (!revokeResult.IsSuccess)
        {
            return Fail("invalid_grant", revokeResult.Message);
        }

        var response = await IssueTokenResponseAsync(
            subjectService,
            tokenService,
            signingKeyService,
            authorityOptions,
            oauthOptions,
            subject,
            scope,
            idTokenAudience: null,
            nonce: null,
            cancellationToken).ConfigureAwait(false);

        return response is null
            ? Fail("invalid_grant", "refresh token subject is invalid.")
            : Result.Success(response);
    }

    private static async ValueTask<OAuthTokenEndpointResponse?> IssueTokenResponseAsync(
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        AuthenticationAuthorityOptions authorityOptions,
        OAuthAuthorizationServerOptions oauthOptions,
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

        var issueResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = oauthOptions.AccessTokenAudience,
                AccessTokenExpiration = oauthOptions.AccessTokenExpiration,
                AccessClaims = accessClaims.ToArray(),
                RefreshToken = new RefreshTokenOptions
                {
                    Expiration = oauthOptions.RefreshTokenExpiration,
                    Claims =
                    [
                        new TokenClaim(OAuthClaimTypes.Subject, subject),
                        new TokenClaim(OAuthClaimTypes.Scope, string.Join(' ', scopes))
                    ]
                }
            },
            cancellationToken).ConfigureAwait(false);
        if (!issueResult.IsSuccess || issueResult.Value.RefreshToken is null)
        {
            return null;
        }

        var idToken = await GenerateIdTokenAsync(
            signingKeyService,
            authorityOptions,
            oauthOptions,
            profile,
            scopes,
            idTokenAudience,
            nonce,
            cancellationToken).ConfigureAwait(false);

        return new OAuthTokenEndpointResponse
        {
            AccessToken = issueResult.Value.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = (long)oauthOptions.AccessTokenExpiration.TotalSeconds,
            RefreshToken = issueResult.Value.RefreshToken.Token,
            Scope = string.Join(' ', scopes),
            IdToken = idToken
        };
    }

    private static async ValueTask<string?> GenerateIdTokenAsync(
        ISigningKeyService signingKeyService,
        AuthenticationAuthorityOptions authorityOptions,
        OAuthAuthorizationServerOptions oauthOptions,
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
        var token = new JwtSecurityToken(
            issuer: authorityOptions.Issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(oauthOptions.AccessTokenExpiration),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async ValueTask<ClaimsPrincipal?> ValidateAccessTokenAsync(
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

    private static async ValueTask<string> RedirectWithCodeAsync(
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

        return AddQueryString(
            request.RedirectUri,
            new Dictionary<string, string?>
            {
                ["code"] = code,
                ["state"] = request.State
            });
    }

    private static AspNetResult CreateAuthorizeFailureResult(OAuthAuthorizationRequest request, OAuthError error)
    {
        if (!string.IsNullOrWhiteSpace(request.ClientId)
            && Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
        {
            return Results.Redirect(
                AddQueryString(
                    request.RedirectUri,
                    new Dictionary<string, string?>
                    {
                        ["error"] = error.Error,
                        ["error_description"] = error.ErrorDescription,
                        ["state"] = request.State
                    }));
        }

        return CreateOAuthErrorResult(error.Error, error.ErrorDescription);
    }

    private static OAuthError? ValidateAuthorizationRequest(OAuthAuthorizationRequest request)
    {
        if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
        {
            return CreateOAuthError("unsupported_response_type", "Only response_type=code is supported.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return CreateOAuthError("invalid_request", "client_id is required.");
        }

        if (!Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
        {
            return CreateOAuthError("invalid_request", "redirect_uri must be an absolute URI.");
        }

        var pkceValidation = ValidateCodeChallenge(request.CodeChallenge, request.CodeChallengeMethod);
        return pkceValidation is null ? null : CreateOAuthError("invalid_request", pkceValidation);
    }

    private static OAuthServerMetadata BuildMetadata(OAuthAuthorizationServerOptions options)
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

    private static string ResolveIssuer(OAuthAuthorizationServerOptions options)
        => options.Issuer.TrimEnd('/');

    private static string NormalizePathBase(string? pathBase)
    {
        if (string.IsNullOrWhiteSpace(pathBase))
        {
            return "/oauth2";
        }

        return pathBase.StartsWith("/", StringComparison.Ordinal)
            ? pathBase.TrimEnd('/')
            : $"/{pathBase.TrimEnd('/')}";
    }

    private static string BuildOAuthAuthorizationServerMetadataRoute(string pathBase)
    {
        var issuerPath = pathBase.TrimEnd('/');
        return string.IsNullOrEmpty(issuerPath) || issuerPath == "/"
            ? "/.well-known/oauth-authorization-server"
            : $"/.well-known/oauth-authorization-server{issuerPath}";
    }

    private static string CombineRoute(string? prefix, string route)
    {
        var normalizedPrefix = string.IsNullOrWhiteSpace(prefix)
            ? string.Empty
            : (prefix.StartsWith("/", StringComparison.Ordinal) ? prefix.TrimEnd('/') : $"/{prefix.TrimEnd('/')}");
        var normalizedRoute = string.IsNullOrWhiteSpace(route)
            ? string.Empty
            : (route.StartsWith("/", StringComparison.Ordinal) ? route : $"/{route}");

        return $"{normalizedPrefix}{normalizedRoute}";
    }

    private static string? ValidateCodeChallenge(string? codeChallenge, string? codeChallengeMethod)
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

    private static string? ValidateCodeVerifier(OidcAuthorizationCodeCacheValue code, string codeVerifier)
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

    private static IReadOnlySet<string> ParseScopes(string scope)
        => scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    private static string NormalizeScope(string scope)
        => string.Join(' ', ParseScopes(scope).OrderBy(static value => value, StringComparer.Ordinal));

    private static string NormalizeCodeChallengeMethod(string? codeChallengeMethod)
        => string.IsNullOrWhiteSpace(codeChallengeMethod) ? "plain" : codeChallengeMethod.Trim();

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static bool ShouldEmitIdentityClaim(string claimType, IReadOnlySet<string> scopes)
    {
        return claimType switch
        {
            OAuthClaimTypes.Email => scopes.Contains(OAuthScope.Email),
            OAuthClaimTypes.Name or OAuthClaimTypes.Groups => scopes.Contains(OAuthScope.Profile),
            OAuthClaimTypes.Scope or OAuthClaimTypes.SessionId => false,
            _ => true
        };
    }

    private static OAuthError CreateOAuthError(string error, string description)
        => new()
        {
            Error = error,
            ErrorDescription = description
        };

    private static AspNetResult CreateOAuthErrorResult(string error, string? description, int statusCode = StatusCodes.Status400BadRequest)
        => Results.Json(
            new OAuthError
            {
                Error = error,
                ErrorDescription = description ?? string.Empty
            },
            statusCode: statusCode);

    private static Result<OAuthTokenEndpointResponse> Fail(string code, string message)
        => Result.Fail(code, message);

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string AddQueryString(string uri, IReadOnlyDictionary<string, string?> query)
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

    private static async ValueTask<string> ResolveBearerTokenAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (BearerToken.TryParse(request.Headers.Authorization.ToString(), provider: null, out var headerToken))
        {
            return headerToken.Value;
        }

        if (HttpMethods.IsPost(request.Method) && request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            if (BearerToken.TryParse(form["access_token"], provider: null, out var formToken))
            {
                return formToken.Value;
            }
        }

        return string.Empty;
    }

    private sealed record OidcAuthorizationCodeCacheKey(string Code)
        : CacheKey<OidcAuthorizationCodeCacheValue>($"nof:oauth:auth-code:{Code}");

    private sealed record OidcAuthorizationCodeCacheValue
    {
        public required string Subject { get; init; }

        public required string ClientId { get; init; }

        public required string RedirectUri { get; init; }

        public required string Scope { get; init; }

        public string? Nonce { get; init; }

        public string? CodeChallenge { get; init; }

        public string? CodeChallengeMethod { get; init; }

        public required DateTime ExpiresAtUtc { get; init; }
    }

    private sealed record OidcRedeemedAuthorizationCodeCacheKey(string Code)
        : CacheKey<OidcRedeemedAuthorizationCodeCacheValue>($"nof:oauth:auth-code:redeemed:{Code}");

    private sealed record OidcRedeemedAuthorizationCodeCacheValue
    {
        public required string ClientId { get; init; }

        public required string RedirectUri { get; init; }

        public required OAuthTokenEndpointResponse Response { get; init; }
    }
}
