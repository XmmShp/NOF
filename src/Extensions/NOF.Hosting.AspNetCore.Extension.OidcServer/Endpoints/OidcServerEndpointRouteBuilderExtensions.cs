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
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AspNetResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.AspNetCore.Routing;

public static partial class NOFOidcServerExtensions
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapOidcServer(string? prefix = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            var mappingState = app.ServiceProvider.GetService<OidcServerEndpointMappingState>();
            if (mappingState is not null && !mappingState.TryMarkMapped())
            {
                return app;
            }

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
            group.MapPost("/token", TokenAsync).DisableAntiforgery();
            group.MapGet("/userinfo", UserInfoAsync);
            group.MapPost("/userinfo", UserInfoAsync).DisableAntiforgery();

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
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery(Name = "scope")] string scope,
        [FromQuery(Name = "state")] string state,
        [FromQuery(Name = "nonce")] string? nonce,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromServices] IOAuthAuthorizationHandler authorizationHandler,
        [FromServices] IOAuthAuthorizationCodeService authorizationCodeService,
        CancellationToken cancellationToken)
    {
        var request = new OAuthAuthorizeRequest
        {
            ResponseType = responseType,
            ClientId = clientId,
            RedirectUri = redirectUri,
            Scope = scope,
            State = state,
            Nonce = nonce,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod
        };

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
        HttpRequest httpRequest,
        [FromForm(Name = "grant_type")] string grantType,
        [FromForm(Name = "code")] string? code,
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm(Name = "redirect_uri")] string? redirectUri,
        [FromForm(Name = "code_verifier")] string? codeVerifier,
        [FromForm(Name = "refresh_token")] string? refreshToken,
        [FromForm(Name = "scope")] string? scope,
        [FromForm(Name = "subject_token")] string? subjectToken,
        [FromForm(Name = "subject_token_type")] string? subjectTokenType,
        [FromForm(Name = "actor_token")] string? actorToken,
        [FromForm(Name = "actor_token_type")] string? actorTokenType,
        [FromForm(Name = "requested_token_type")] string? requestedTokenType,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] ICacheService cacheService,
        [FromServices] IOAuthSubjectService subjectService,
        [FromServices] ITokenService tokenService,
        [FromServices] ISigningKeyService signingKeyService,
        [FromServices] IOptions<OAuthAuthorizationServerOptions> oauthOptions,
        CancellationToken cancellationToken)
    {
        var request = new OAuthTokenRequest
        {
            GrantType = grantType,
            Code = code ?? string.Empty,
            ClientId = clientId ?? string.Empty,
            ClientSecret = clientSecret ?? string.Empty,
            RedirectUri = redirectUri ?? string.Empty,
            CodeVerifier = codeVerifier ?? string.Empty,
            RefreshToken = refreshToken ?? string.Empty,
            Scope = scope ?? string.Empty,
            SubjectToken = subjectToken ?? string.Empty,
            SubjectTokenType = subjectTokenType ?? string.Empty,
            ActorToken = actorToken ?? string.Empty,
            ActorTokenType = actorTokenType ?? string.Empty,
            RequestedTokenType = requestedTokenType ?? string.Empty
        };
        ApplyResolvedClientCredentials(httpRequest, request);

        var result = request.GrantType switch
        {
            OAuthGrantTypes.AuthorizationCode => await TokenFromAuthorizationCodeAsync(
                httpRequest,
                request,
                serviceProvider,
                cacheService,
                subjectService,
                tokenService,
                signingKeyService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            OAuthGrantTypes.ClientCredentials => await TokenFromClientCredentialsAsync(
                httpRequest,
                request,
                serviceProvider,
                tokenService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            OAuthGrantTypes.RefreshToken => await TokenFromRefreshTokenAsync(
                httpRequest,
                request,
                serviceProvider,
                subjectService,
                tokenService,
                signingKeyService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            OAuthGrantTypes.TokenExchange => await TokenFromTokenExchangeAsync(
                httpRequest,
                request,
                serviceProvider,
                subjectService,
                tokenService,
                signingKeyService,
                oauthOptions.Value,
                cancellationToken).ConfigureAwait(false),
            _ => Fail("unsupported_grant_type", "Only authorization_code, client_credentials, refresh_token, and token-exchange are supported.")
        };

        return result.IsSuccess
            ? Results.Json(result.Value)
            : CreateOAuthErrorResult(result.ErrorCode, result.Message);
    }

    private static async Task<AspNetResult> UserInfoAsync(
        HttpRequest httpRequest,
        [FromServices] IOAuthSubjectService subjectService,
        [FromServices] ISigningKeyService signingKeyService,
        [FromServices] IOptions<OAuthAuthorizationServerOptions> oauthOptions,
        CancellationToken cancellationToken)
    {
        var accessToken = await ResolveBearerTokenAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var principal = await ValidateAccessTokenAsync(
            accessToken,
            signingKeyService,
            oauthOptions.Value,
            oauthOptions.Value.AccessTokenAudience,
            cancellationToken).ConfigureAwait(false);
        var subject = principal?.FindFirst(OAuthClaimTypes.Subject)?.Value;
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

    internal static async Task<Result<OAuthTokenEndpointResponse>> TokenFromAuthorizationCodeAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        ICacheService cacheService,
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        OAuthAuthorizationServerOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Fail("invalid_request", "code is required.");
        }

        var authenticationError = await ValidateClientAuthenticationAsync(
            httpRequest,
            request,
            serviceProvider,
            cancellationToken).ConfigureAwait(false);
        if (authenticationError is not null)
        {
            return Fail(authenticationError.Error, authenticationError.ErrorDescription);
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
            oauthOptions,
            authorizationCode.Subject,
            authorizationCode.Scope,
            authorizationCode.ClientId,
            authorizationCode.ClientId,
            authorizationCode.Nonce,
            additionalAccessClaims: null,
            issueRefreshToken: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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

    internal static async Task<Result<OAuthTokenEndpointResponse>> TokenFromRefreshTokenAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        OAuthAuthorizationServerOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Fail("invalid_request", "refresh_token is required.");
        }

        var authenticationError = await ValidateClientAuthenticationAsync(
            httpRequest,
            request,
            serviceProvider,
            cancellationToken).ConfigureAwait(false);
        if (authenticationError is not null)
        {
            return Fail(authenticationError.Error, authenticationError.ErrorDescription);
        }

        var validateResult = await tokenService.ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest
            {
                RefreshToken = request.RefreshToken,
                Audience = oauthOptions.AccessTokenAudience
            },
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
        var refreshClientId = claims.TryGetValue("client_id", out var clientIdClaim) ? clientIdClaim : string.Empty;
        if (string.IsNullOrWhiteSpace(refreshClientId))
        {
            return Fail("invalid_grant", "refresh token client is invalid.");
        }

        if (!FixedTimeEquals(refreshClientId, request.ClientId))
        {
            return Fail("invalid_grant", "refresh token client does not match.");
        }

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
            oauthOptions,
            subject,
            scope,
            refreshClientId,
            idTokenAudience: null,
            nonce: null,
            additionalAccessClaims: null,
            issueRefreshToken: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response is null
            ? Fail("invalid_grant", "refresh token subject is invalid.")
            : Result.Success(response);
    }

    private static async ValueTask<OAuthTokenEndpointResponse?> IssueTokenResponseAsync(
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        OAuthAuthorizationServerOptions oauthOptions,
        string subject,
        string scope,
        string refreshTokenClientId,
        string? idTokenAudience,
        string? nonce,
        IReadOnlyList<KeyValuePair<string, string>>? additionalAccessClaims,
        bool issueRefreshToken,
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
        if (additionalAccessClaims is not null)
        {
            accessClaims.AddRange(additionalAccessClaims.Select(static claim => new TokenClaim(claim.Key, claim.Value)));
        }

        var issueResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = oauthOptions.AccessTokenAudience,
                AccessTokenExpiration = oauthOptions.AccessTokenExpiration,
                AccessClaims = accessClaims.ToArray(),
                RefreshToken = issueRefreshToken
                    ? new RefreshTokenOptions
                    {
                        Expiration = oauthOptions.RefreshTokenExpiration,
                        Claims =
                        [
                            new TokenClaim(OAuthClaimTypes.Subject, subject),
                            new TokenClaim(OAuthClaimTypes.Scope, string.Join(' ', scopes)),
                            new TokenClaim("client_id", refreshTokenClientId)
                        ]
                    }
                    : null
            },
            cancellationToken).ConfigureAwait(false);
        if (!issueResult.IsSuccess || (issueRefreshToken && issueResult.Value.RefreshToken is null))
        {
            return null;
        }

        var idToken = await GenerateIdTokenAsync(
            signingKeyService,
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
            RefreshToken = issueResult.Value.RefreshToken?.Token,
            Scope = string.Join(' ', scopes),
            IdToken = idToken
        };
    }

    internal static async Task<Result<OAuthTokenEndpointResponse>> TokenFromTokenExchangeAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        OAuthAuthorizationServerOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        var authenticationError = await ValidateClientAuthenticationAsync(
            httpRequest,
            request,
            serviceProvider,
            cancellationToken).ConfigureAwait(false);
        if (authenticationError is not null)
        {
            return Fail(authenticationError.Error, authenticationError.ErrorDescription);
        }

        if (string.IsNullOrWhiteSpace(request.SubjectToken))
        {
            return Fail("invalid_request", "subject_token is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ActorToken))
        {
            return Fail("invalid_request", "actor_token is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectTokenType)
            && !string.Equals(request.SubjectTokenType, OAuthTokenTypes.AccessToken, StringComparison.Ordinal))
        {
            return Fail("invalid_request", "Only access_token subject_token_type is supported.");
        }

        if (!string.IsNullOrWhiteSpace(request.ActorTokenType)
            && !string.Equals(request.ActorTokenType, OAuthTokenTypes.AccessToken, StringComparison.Ordinal))
        {
            return Fail("invalid_request", "Only access_token actor_token_type is supported.");
        }

        if (!string.IsNullOrWhiteSpace(request.RequestedTokenType)
            && !string.Equals(request.RequestedTokenType, OAuthTokenTypes.AccessToken, StringComparison.Ordinal))
        {
            return Fail("invalid_request", "Only access_token requested_token_type is supported.");
        }

        var principal = await ValidateAccessTokenAsync(
            request.SubjectToken,
            signingKeyService,
            oauthOptions,
            oauthOptions.AccessTokenAudience,
            cancellationToken).ConfigureAwait(false);
        if (principal is null)
        {
            return Fail("invalid_grant", "subject_token is invalid.");
        }

        var subject = principal.FindFirst(OAuthClaimTypes.Subject)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Fail("invalid_grant", "subject_token subject is invalid.");
        }

        var actorPrincipal = await ValidateAccessTokenAsync(
            request.ActorToken,
            signingKeyService,
            oauthOptions,
            oauthOptions.AccessTokenAudience,
            cancellationToken).ConfigureAwait(false);
        if (actorPrincipal is null)
        {
            return Fail("invalid_grant", "actor_token is invalid.");
        }

        var proxyServiceName = ResolveProxyServiceName(actorPrincipal);
        if (string.IsNullOrWhiteSpace(proxyServiceName))
        {
            return Fail("invalid_grant", "actor_token subject is invalid.");
        }

        var grantedScopes = ParseScopes(principal.FindFirst(OAuthClaimTypes.Scope)?.Value ?? string.Empty);
        var actorScopes = ParseScopes(actorPrincipal.FindFirst(OAuthClaimTypes.Scope)?.Value ?? string.Empty);
        var requestedScopes = ParseScopes(request.Scope);
        var scopes = grantedScopes.Where(actorScopes.Contains).ToHashSet(StringComparer.Ordinal);
        if (requestedScopes.Count > 0)
        {
            scopes.IntersectWith(requestedScopes);
        }

        if (scopes.Count == 0)
        {
            return Fail("invalid_scope", "requested scope is not granted by subject_token and actor_token.");
        }

        var response = await IssueTokenResponseAsync(
            subjectService,
            tokenService,
            signingKeyService,
            oauthOptions,
            subject,
            string.Join(' ', scopes.OrderBy(static value => value, StringComparer.Ordinal)),
            string.Empty,
            idTokenAudience: null,
            nonce: null,
            additionalAccessClaims:
                [new KeyValuePair<string, string>(ClaimTypes.ProxyServiceName, proxyServiceName)],
            issueRefreshToken: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response is null
            ? Fail("invalid_grant", "subject_token subject is invalid.")
            : Result.Success(response);
    }

    private static string? ResolveProxyServiceName(ClaimsPrincipal actorPrincipal)
        => actorPrincipal.FindFirst("client_id")?.Value
            ?? actorPrincipal.FindFirst(OAuthClaimTypes.Subject)?.Value;

    internal static async Task<Result<OAuthTokenEndpointResponse>> TokenFromClientCredentialsAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        ITokenService tokenService,
        OAuthAuthorizationServerOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        var clientService = ResolveService<IOAuthClientManagementService>(serviceProvider);
        if (clientService is null)
        {
            return Fail("server_error", "OAuth client management service is not registered.");
        }

        var clientCredentials = ResolveClientCredentials(httpRequest, request);
        if (string.IsNullOrWhiteSpace(clientCredentials.ClientId))
        {
            return Fail("invalid_client", "client_id is required.");
        }

        var requestedScopes = ParseScopes(request.Scope);
        var validation = await clientService.ValidateClientCredentialsAsync(
            new OAuthClientCredentialsValidationRequest(
                clientCredentials.ClientId,
                clientCredentials.ClientSecret,
                requestedScopes,
                clientCredentials.AuthenticationMethod),
            cancellationToken).ConfigureAwait(false);

        if (validation is OAuthClientCredentialsValidationResult.Failure failure)
        {
            return Fail(failure.Error, failure.ErrorDescription);
        }

        if (validation is not OAuthClientCredentialsValidationResult.Success success)
        {
            return Fail("invalid_client", "client credentials are invalid.");
        }

        var scopes = success.Scopes.Count == 0 ? requestedScopes : success.Scopes;
        var scopeText = string.Join(' ', scopes.OrderBy(static value => value, StringComparer.Ordinal));
        var accessClaims = success.AccessTokenClaims
            .Select(static claim => new TokenClaim(claim.Key, claim.Value))
            .ToList();
        if (accessClaims.All(static claim => claim.Type != OAuthClaimTypes.Subject))
        {
            accessClaims.Insert(0, new TokenClaim(OAuthClaimTypes.Subject, success.Subject));
        }

        accessClaims.Add(new TokenClaim(OAuthClaimTypes.Scope, scopeText));

        var issueResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = oauthOptions.AccessTokenAudience,
                AccessTokenExpiration = oauthOptions.AccessTokenExpiration,
                AccessClaims = accessClaims.ToArray()
            },
            cancellationToken).ConfigureAwait(false);
        if (!issueResult.IsSuccess)
        {
            return Fail(issueResult.ErrorCode, issueResult.Message);
        }

        return Result.Success(new OAuthTokenEndpointResponse
        {
            AccessToken = issueResult.Value.AccessToken,
            TokenType = "Bearer",
            ExpiresIn = (long)oauthOptions.AccessTokenExpiration.TotalSeconds,
            Scope = scopeText
        });
    }

    private static async ValueTask<string?> GenerateIdTokenAsync(
        ISigningKeyService signingKeyService,
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
            issuer: oauthOptions.Issuer,
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
        OAuthAuthorizationServerOptions options,
        string audience,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };

            return tokenHandler.ValidateToken(
                token.Trim(),
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = (await signingKeyService.GetAllKeysAsync(cancellationToken).ConfigureAwait(false)).Select(static key => key.Key),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(options.Issuer),
                    ValidIssuer = options.Issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
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
            GrantTypesSupported = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.ClientCredentials, OAuthGrantTypes.RefreshToken, OAuthGrantTypes.TokenExchange],
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

    private static TService? ResolveService<TService>(IServiceProvider serviceProvider)
    {
        try
        {
            return serviceProvider.GetService<TService>();
        }
        catch (InvalidOperationException)
        {
            return default;
        }
    }

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    internal static void ApplyResolvedClientCredentials(HttpRequest httpRequest, OAuthTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        ArgumentNullException.ThrowIfNull(request);

        var clientCredentials = ResolveClientCredentials(httpRequest, request);
        request.ClientId = clientCredentials.ClientId;
        request.ClientSecret = clientCredentials.ClientSecret ?? string.Empty;
    }

    internal static async Task<OAuthError?> ValidateClientAuthenticationAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var clientService = ResolveService<IOAuthClientManagementService>(serviceProvider);
        if (clientService is null)
        {
            return null;
        }

        var clientCredentials = ResolveClientCredentials(httpRequest, request);
        if (string.IsNullOrWhiteSpace(clientCredentials.ClientId))
        {
            return CreateOAuthError("invalid_client", "client_id is required.");
        }

        var clientResult = await clientService.GetAsync(clientCredentials.ClientId, cancellationToken).ConfigureAwait(false);
        if (!clientResult.IsSuccess || !clientResult.Value.IsEnabled)
        {
            return CreateOAuthError("invalid_client", "client credentials are invalid.");
        }

        if (clientResult.Value.ClientType == OAuthClientType.Public)
        {
            return ValidatePublicClientAuthentication(request, clientCredentials);
        }

        if (string.IsNullOrWhiteSpace(clientCredentials.ClientSecret))
        {
            return CreateOAuthError("invalid_client", "client_secret is required.");
        }

        var validation = await clientService.ValidateClientCredentialsAsync(
            new OAuthClientCredentialsValidationRequest(
                clientCredentials.ClientId,
                clientCredentials.ClientSecret,
                ParseScopes(request.Scope),
                clientCredentials.AuthenticationMethod),
            cancellationToken).ConfigureAwait(false);

        return validation switch
        {
            OAuthClientCredentialsValidationResult.Success => null,
            OAuthClientCredentialsValidationResult.Failure failure => CreateOAuthError(failure.Error, failure.ErrorDescription),
            _ => CreateOAuthError("invalid_client", "client credentials are invalid.")
        };
    }

    private static OAuthError? ValidatePublicClientAuthentication(
        OAuthTokenRequest request,
        (string ClientId, string? ClientSecret, string AuthenticationMethod) clientCredentials)
    {
        if (!string.IsNullOrWhiteSpace(clientCredentials.ClientSecret))
        {
            return CreateOAuthError("invalid_client", "public clients must not use client_secret.");
        }

        if (string.Equals(request.GrantType, OAuthGrantTypes.AuthorizationCode, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(request.CodeVerifier)
                ? CreateOAuthError("invalid_client", "code_verifier is required for public clients.")
                : null;
        }

        if (string.Equals(request.GrantType, OAuthGrantTypes.RefreshToken, StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(request.GrantType, OAuthGrantTypes.TokenExchange, StringComparison.Ordinal))
        {
            return null;
        }

        return CreateOAuthError("invalid_client", "public client authentication is invalid for this grant type.");
    }

    private static (string ClientId, string? ClientSecret, string AuthenticationMethod) ResolveClientCredentials(
        HttpRequest httpRequest,
        OAuthTokenRequest request)
    {
        if (TryResolveBasicClientCredentials(httpRequest.Headers.Authorization.ToString(), out var basicCredentials))
        {
            return (basicCredentials.ClientId, basicCredentials.ClientSecret, "client_secret_basic");
        }

        return (request.ClientId, EmptyToNull(request.ClientSecret), "client_secret_post");
    }

    private static bool TryResolveBasicClientCredentials(
        string authorization,
        out (string ClientId, string? ClientSecret) credentials)
    {
        credentials = default;
        const string prefix = "Basic ";
        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[prefix.Length..].Trim()));
            var separatorIndex = decoded.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return false;
            }

            credentials = (
                Uri.UnescapeDataString(decoded[..separatorIndex]),
                Uri.UnescapeDataString(decoded[(separatorIndex + 1)..]));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

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
