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
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            var pathBase = string.IsNullOrWhiteSpace(options.PathBase)
                ? "/oauth2"
                : options.PathBase.StartsWith("/", StringComparison.Ordinal)
                    ? options.PathBase.TrimEnd('/')
                    : $"/{options.PathBase.TrimEnd('/')}";
            var group = app.MapGroup(CombineRoute(prefix, pathBase));

            group.MapGet(string.Empty, static (IOAuthServerRootEndpoint endpoint, CancellationToken cancellationToken)
                => endpoint.HandleAsync(cancellationToken));

            app.MapGet(CombineRoute(prefix, "/.well-known/openid-configuration"),
                static (IOAuthMetadataEndpoint endpoint, CancellationToken cancellationToken) => endpoint.HandleAsync(cancellationToken));
            app.MapGet(CombineRoute(prefix, $"{pathBase}/.well-known/openid-configuration"),
                static (IOAuthMetadataEndpoint endpoint, CancellationToken cancellationToken) => endpoint.HandleAsync(cancellationToken));

            app.MapGet(CombineRoute(prefix, "/.well-known/oauth-authorization-server"),
                static (IOAuthMetadataEndpoint endpoint, CancellationToken cancellationToken) => endpoint.HandleAsync(cancellationToken));

            var issuerPath = pathBase.TrimEnd('/');
            var authorizationServerMetadataRoute = string.IsNullOrEmpty(issuerPath) || issuerPath == "/"
                ? "/.well-known/oauth-authorization-server"
                : $"/.well-known/oauth-authorization-server{issuerPath}";
            app.MapGet(CombineRoute(prefix, authorizationServerMetadataRoute),
                static (IOAuthMetadataEndpoint endpoint, CancellationToken cancellationToken) => endpoint.HandleAsync(cancellationToken));

            app.MapGet(CombineRoute(prefix, "/.well-known/jwks.json"),
                static (IOAuthJwksEndpoint endpoint, CancellationToken cancellationToken)
                    => endpoint.HandleAsync(cancellationToken));
            app.MapGet(CombineRoute(prefix, $"{pathBase}/.well-known/jwks.json"),
                static (IOAuthJwksEndpoint endpoint, CancellationToken cancellationToken)
                    => endpoint.HandleAsync(cancellationToken));

            group.MapGet("/authorize", AuthorizeAsync);
            group.MapPost("/device_authorization", DeviceAuthorizationAsync).DisableAntiforgery();
            group.MapMethods("/device", [HttpMethods.Get, HttpMethods.Post], DeviceVerificationAsync);
            group.MapPost("/token", TokenAsync).DisableAntiforgery();
            group.MapPost("/revoke", RevokeAsync).DisableAntiforgery();
            group.MapPost("/introspect", IntrospectAsync).DisableAntiforgery();
            group.MapGet("/userinfo", UserInfoAsync);
            group.MapPost("/userinfo", UserInfoAsync).DisableAntiforgery();
            group.MapPost("/register", RegisterClientAsync).DisableAntiforgery();
            group.MapGet("/register/{clientId}", GetClientRegistrationAsync);
            group.MapPut("/register/{clientId}", UpdateClientRegistrationAsync).DisableAntiforgery();
            group.MapDelete("/register/{clientId}", DeleteClientRegistrationAsync).DisableAntiforgery();

            return app;
        }
    }

    private static async Task<AspNetResult> AuthorizeAsync(
        HttpRequest httpRequest,
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery(Name = "scope")] string scope,
        [FromQuery(Name = "state")] string state,
        [FromQuery(Name = "nonce")] string? nonce,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromServices] IOAuthAuthorizeEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var request = new OAuthAuthorizeRequest
        {
            ResponseType = responseType,
            ClientId = clientId,
            RedirectUri = redirectUri ?? string.Empty,
            Scope = scope,
            State = state,
            Nonce = nonce,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod
        };
        var wasRedirectUriSupplied = !string.IsNullOrWhiteSpace(request.RedirectUri);

        var authorizationRequest = new OAuthAuthorizationRequest(
            ResponseType: request.ResponseType,
            ClientId: request.ClientId,
            RedirectUri: request.RedirectUri,
            Scope: NormalizeScope(request.Scope),
            State: request.State,
            Nonce: EmptyToNull(request.Nonce),
            CodeChallenge: EmptyToNull(request.CodeChallenge),
            CodeChallengeMethod: EmptyToNull(request.CodeChallengeMethod));
        var validation = await ValidateAuthorizationRequestAsync(
            httpRequest.HttpContext.RequestServices,
            authorizationRequest,
            cancellationToken).ConfigureAwait(false);
        if (validation.Error is not null)
        {
            return CreateAuthorizeFailureResult(validation.Request, validation.Error, validation.AllowRedirect);
        }

        request.ResponseType = validation.Request.ResponseType;
        request.RedirectUri = validation.Request.RedirectUri;
        request.Scope = validation.Request.Scope;
        request.Nonce = validation.Request.Nonce;
        request.CodeChallenge = validation.Request.CodeChallenge;
        request.CodeChallengeMethod = validation.Request.CodeChallengeMethod;

        return await endpoint.HandleAsync(
            new OAuthAuthorizeEndpointRequest(request, wasRedirectUriSupplied),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AspNetResult> TokenAsync(
        HttpRequest httpRequest,
        [FromForm(Name = "grant_type")] string grantType,
        [FromForm(Name = "code")] string? code,
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm(Name = "client_assertion_type")] string? clientAssertionType,
        [FromForm(Name = "client_assertion")] string? clientAssertion,
        [FromForm(Name = "redirect_uri")] string? redirectUri,
        [FromForm(Name = "code_verifier")] string? codeVerifier,
        [FromForm(Name = "refresh_token")] string? refreshToken,
        [FromForm(Name = "device_code")] string? deviceCode,
        [FromForm(Name = "scope")] string? scope,
        [FromForm(Name = "subject_token")] string? subjectToken,
        [FromForm(Name = "subject_token_type")] string? subjectTokenType,
        [FromForm(Name = "actor_token")] string? actorToken,
        [FromForm(Name = "actor_token_type")] string? actorTokenType,
        [FromForm(Name = "requested_token_type")] string? requestedTokenType,
        [FromServices] IOAuthTokenEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var request = new OAuthTokenRequest
        {
            GrantType = grantType,
            Code = code ?? string.Empty,
            ClientId = clientId ?? string.Empty,
            ClientSecret = clientSecret ?? string.Empty,
            ClientAssertionType = clientAssertionType ?? string.Empty,
            ClientAssertion = clientAssertion ?? string.Empty,
            RedirectUri = redirectUri ?? string.Empty,
            CodeVerifier = codeVerifier ?? string.Empty,
            RefreshToken = refreshToken ?? string.Empty,
            DeviceCode = deviceCode ?? string.Empty,
            Scope = scope ?? string.Empty,
            SubjectToken = subjectToken ?? string.Empty,
            SubjectTokenType = subjectTokenType ?? string.Empty,
            ActorToken = actorToken ?? string.Empty,
            ActorTokenType = actorTokenType ?? string.Empty,
            RequestedTokenType = requestedTokenType ?? string.Empty
        };
        ApplyResolvedClientCredentials(httpRequest, request);

        return await endpoint.HandleAsync(
            new OAuthTokenEndpointRequest(httpRequest, request),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AspNetResult> DeviceAuthorizationAsync(
        HttpRequest httpRequest,
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm(Name = "client_assertion_type")] string? clientAssertionType,
        [FromForm(Name = "client_assertion")] string? clientAssertion,
        [FromForm(Name = "scope")] string? scope,
        [FromServices] IOAuthDeviceAuthorizationEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var request = new OAuthDeviceAuthorizationRequest
        {
            ClientId = clientId ?? string.Empty,
            ClientSecret = clientSecret ?? string.Empty,
            ClientAssertionType = clientAssertionType ?? string.Empty,
            ClientAssertion = clientAssertion ?? string.Empty,
            Scope = scope ?? string.Empty
        };
        return await endpoint.HandleAsync(
            new OAuthDeviceAuthorizationEndpointRequest(httpRequest, request),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AspNetResult> DeviceVerificationAsync(
        HttpRequest httpRequest,
        [FromServices] IOAuthDeviceVerificationEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        httpRequest.HttpContext.Response.Headers.CacheControl = "no-store";
        httpRequest.HttpContext.Response.Headers.Pragma = "no-cache";
        httpRequest.HttpContext.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        httpRequest.HttpContext.Response.Headers.XFrameOptions = "DENY";
        httpRequest.HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        return await endpoint.HandleAsync(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AspNetResult> UserInfoAsync(
        HttpRequest httpRequest,
        [FromServices] IOAuthUserInfoEndpoint endpoint,
        CancellationToken cancellationToken)
        => await endpoint.HandleAsync(httpRequest, cancellationToken).ConfigureAwait(false);

    private static async Task<AspNetResult> RevokeAsync(
        HttpRequest httpRequest,
        [FromForm(Name = "token")] string? token,
        [FromForm(Name = "token_type_hint")] string? tokenTypeHint,
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromServices] IOAuthRevokeEndpoint endpoint,
        CancellationToken cancellationToken)
        => await endpoint.HandleAsync(
            new OAuthRevokeEndpointRequest(httpRequest, token, tokenTypeHint, clientId, clientSecret),
            cancellationToken).ConfigureAwait(false);

    private static async Task<AspNetResult> IntrospectAsync(
        HttpRequest httpRequest,
        [FromForm(Name = "token")] string? token,
        [FromForm(Name = "token_type_hint")] string? tokenTypeHint,
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromServices] IOAuthIntrospectEndpoint endpoint,
        CancellationToken cancellationToken)
        => await endpoint.HandleAsync(
            new OAuthIntrospectEndpointRequest(httpRequest, token, tokenTypeHint, clientId, clientSecret),
            cancellationToken).ConfigureAwait(false);

    private static async Task<AspNetResult> RegisterClientAsync(
        HttpRequest httpRequest,
        [FromBody] OAuthClientRegistrationRequest request,
        [FromServices] IOAuthClientRegistrationEndpoint endpoint,
        CancellationToken cancellationToken)
        => await endpoint.RegisterAsync(httpRequest, request, cancellationToken).ConfigureAwait(false);

    private static async Task<AspNetResult> GetClientRegistrationAsync(
        HttpRequest httpRequest,
        string clientId,
        [FromServices] IOAuthClientRegistrationEndpoint endpoint,
        CancellationToken cancellationToken)
        => await endpoint.GetAsync(httpRequest, clientId, cancellationToken).ConfigureAwait(false);

    private static async Task<AspNetResult> UpdateClientRegistrationAsync(
        HttpRequest httpRequest,
        string clientId,
        [FromBody] OAuthClientRegistrationUpdateRequest request,
        [FromServices] IOAuthClientRegistrationEndpoint endpoint,
        CancellationToken cancellationToken)
        => await endpoint.UpdateAsync(httpRequest, clientId, request, cancellationToken).ConfigureAwait(false);

    private static async Task<AspNetResult> DeleteClientRegistrationAsync(
        HttpRequest httpRequest,
        string clientId,
        [FromServices] IOAuthClientRegistrationEndpoint endpoint,
        CancellationToken cancellationToken)
        => await endpoint.DeleteAsync(httpRequest, clientId, cancellationToken).ConfigureAwait(false);

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
                || !RedirectUriMatches(request.RedirectUri, redeemed.Value.RedirectUri, redeemed.Value.WasRedirectUriSupplied))
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
            || !RedirectUriMatches(request.RedirectUri, authorizationCode.RedirectUri, authorizationCode.WasRedirectUriSupplied))
        {
            return Fail("invalid_grant", "authorization code client or redirect_uri does not match.");
        }

        string? verifierError = null;
        if (!string.IsNullOrWhiteSpace(authorizationCode.CodeChallenge))
        {
            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
            {
                verifierError = "code_verifier is required.";
            }
            else
            {
                var expectedChallenge = string.Equals(authorizationCode.CodeChallengeMethod, "S256", StringComparison.Ordinal)
                    ? Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(request.CodeVerifier)))
                    : request.CodeVerifier;

                verifierError = FixedTimeEquals(authorizationCode.CodeChallenge, expectedChallenge)
                    ? null
                    : "code_verifier is invalid.";
            }
        }

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
            issueRefreshToken: ShouldIssueRefreshToken(authorizationCode.Scope),
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
                WasRedirectUriSupplied = authorizationCode.WasRedirectUriSupplied,
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
            .ToDictionary(static group => group.Key, static group => group.First().Value ?? string.Empty, StringComparer.Ordinal);
        if (!claims.TryGetValue(OAuthClaimTypes.Subject, out var subject) || string.IsNullOrWhiteSpace(subject))
        {
            return Fail("invalid_grant", "refresh token subject is invalid.");
        }

        var scope = claims.TryGetValue(OAuthClaimTypes.Scope, out var scopeClaim) ? scopeClaim : string.Empty;
        var refreshClientId = claims.TryGetValue(OAuthClaimTypes.ClientId, out var clientIdClaim) ? clientIdClaim : string.Empty;
        if (string.IsNullOrWhiteSpace(refreshClientId))
        {
            return Fail("invalid_grant", "refresh token client is invalid.");
        }

        if (!FixedTimeEquals(refreshClientId, request.ClientId))
        {
            return Fail("invalid_grant", "refresh token client does not match.");
        }

        var scopes = ParseScopes(scope ?? string.Empty);
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

        var scopeText = scope ?? string.Empty;
        var response = await IssueTokenResponseAsync(
            subjectService,
            tokenService,
            signingKeyService,
            oauthOptions,
            subject,
            scopeText,
            refreshClientId,
            idTokenAudience: null,
            nonce: null,
            additionalAccessClaims: null,
            issueRefreshToken: ShouldIssueRefreshToken(scopeText),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response is null
            ? Fail("invalid_grant", "refresh token subject is invalid.")
            : Result.Success(response);
    }

    internal static async Task<Result<OAuthTokenEndpointResponse>> TokenFromDeviceCodeAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        IOAuthDeviceGrantService deviceGrantService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceCode))
        {
            return Fail("invalid_request", "device_code is required.");
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

        return await deviceGrantService.RedeemAsync(
            request.DeviceCode,
            request.ClientId,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<OAuthTokenEndpointResponse?> IssueTokenResponseAsync(
        IOAuthSubjectService subjectService,
        ITokenService tokenService,
        ISigningKeyService signingKeyService,
        OAuthAuthorizationServerOptions oauthOptions,
        string subject,
        string scope,
        string clientId,
        string? idTokenAudience,
        string? nonce,
        IReadOnlyList<TokenClaim>? additionalAccessClaims,
        bool issueRefreshToken,
        CancellationToken cancellationToken)
    {
        return await new OAuthTokenResponseIssuer(
                subjectService,
                tokenService,
                signingKeyService,
                Options.Create(oauthOptions))
            .IssueAsync(
                subject,
                scope,
                clientId,
                idTokenAudience,
                nonce,
                additionalAccessClaims,
                issueRefreshToken,
                cancellationToken)
            .ConfigureAwait(false);
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

        var authenticatedClient = await AuthenticateClientAsync(
            httpRequest,
            request.ClientId,
            request.ClientSecret,
            request.Scope,
            serviceProvider,
            allowPublicClient: true,
            cancellationToken).ConfigureAwait(false);
        if (authenticatedClient.Error is not null)
        {
            return Fail(authenticatedClient.Error.Error, authenticatedClient.Error.ErrorDescription);
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

        var clientRepository = ResolveService<IOAuthClientRepository>(serviceProvider);
        if (clientRepository is null)
        {
            return Fail("server_error", "OAuth client repository is not registered.");
        }

        var clientResult = await clientRepository.GetAsync(authenticatedClient.ClientId, cancellationToken).ConfigureAwait(false);
        if (!clientResult.IsSuccess || !clientResult.Value.IsEnabled)
        {
            return Fail("invalid_client", "client credentials are invalid.");
        }

        var tokenExchangeHandler = ResolveService<IOAuthTokenExchangeHandler>(serviceProvider);
        if (tokenExchangeHandler is null)
        {
            return Fail("server_error", "OAuth token exchange handler is not registered.");
        }

        var exchangeResult = await tokenExchangeHandler.HandleAsync(
            new OAuthTokenExchangeRequest(
                authenticatedClient.ClientId,
                clientResult.Value.ClientType,
                subject,
                principal,
                actorPrincipal,
                ParseScopes(request.Scope)),
            cancellationToken).ConfigureAwait(false);

        if (exchangeResult is OAuthTokenExchangeResult.Failure failure)
        {
            return Fail(failure.Error, failure.ErrorDescription);
        }

        var success = (OAuthTokenExchangeResult.Success)exchangeResult;
        var response = await IssueTokenResponseAsync(
            subjectService,
            tokenService,
            signingKeyService,
            oauthOptions,
            success.Subject,
            string.Join(' ', success.Scopes.OrderBy(static value => value, StringComparer.Ordinal)),
            authenticatedClient.ClientId,
            idTokenAudience: null,
            nonce: null,
            additionalAccessClaims: success.AccessTokenClaims,
            issueRefreshToken: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response is null
            ? Fail("invalid_grant", "subject_token subject is invalid.")
            : Result.Success(response);
    }
    internal static async Task<Result<OAuthTokenEndpointResponse>> TokenFromClientCredentialsAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        ITokenService tokenService,
        OAuthAuthorizationServerOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        var clientRepository = ResolveService<IOAuthClientRepository>(serviceProvider);
        if (clientRepository is null)
        {
            return Fail("server_error", "OAuth client repository is not registered.");
        }

        var clientCredentials = ResolveClientCredentials(httpRequest, request);
        if (clientCredentials.Error is not null)
        {
            return Fail(clientCredentials.Error.Error, clientCredentials.Error.ErrorDescription);
        }

        if (string.IsNullOrWhiteSpace(clientCredentials.ClientId))
        {
            return Fail("invalid_client", "client_id is required.");
        }

        var requestedScopes = ParseScopes(request.Scope);
        var validation = await clientRepository.ValidateClientCredentialsAsync(
            new OAuthClientCredentialsValidationRequest(
                clientCredentials.ClientId,
                clientCredentials.ClientSecret,
                requestedScopes,
                clientCredentials.AuthenticationMethod,
                EmptyToNull(request.ClientAssertion),
                EmptyToNull(request.ClientAssertionType),
                BuildTokenEndpointAudience(oauthOptions),
                OAuthGrantTypes.ClientCredentials),
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
            var subject = string.IsNullOrWhiteSpace(success.Subject)
                || string.Equals(success.Subject, clientCredentials.ClientId, StringComparison.Ordinal)
                    ? $"client:{clientCredentials.ClientId}"
                    : success.Subject;
            accessClaims.Insert(0, new TokenClaim(OAuthClaimTypes.Subject, subject));
        }

        accessClaims.Add(new TokenClaim(OAuthClaimTypes.Scope, scopeText));

        var issueResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = oauthOptions.AccessTokenAudience,
                ClientId = clientCredentials.ClientId,
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

    internal static async ValueTask<ClaimsPrincipal?> ValidateAccessTokenAsync(
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

    internal static AspNetResult CreateAuthorizeFailureResult(OAuthAuthorizationRequest request, OAuthError error, bool allowRedirect = true)
    {
        if (allowRedirect
            && !string.IsNullOrWhiteSpace(request.ClientId)
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

    internal static async Task<(OAuthAuthorizationRequest Request, OAuthError? Error, bool AllowRedirect)> ValidateAuthorizationRequestAsync(
        IServiceProvider serviceProvider,
        OAuthAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
        {
            return (request, CreateOAuthError("unsupported_response_type", "Only response_type=code is supported."), false);
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return (request, CreateOAuthError("invalid_request", "client_id is required."), false);
        }

        var clientRepository = ResolveService<IOAuthClientRepository>(serviceProvider);
        if (clientRepository is null)
        {
            return (request, CreateOAuthError("server_error", "OAuth client repository is not registered."), false);
        }

        var clientResult = await clientRepository.GetAsync(request.ClientId, cancellationToken).ConfigureAwait(false);
        if (!clientResult.IsSuccess || !clientResult.Value.IsEnabled)
        {
            return (request, CreateOAuthError("invalid_client", "client_id is invalid."), false);
        }

        if (clientResult.Value.AllowedResponseTypes.Count > 0
            && !clientResult.Value.AllowedResponseTypes.Contains(request.ResponseType, StringComparer.Ordinal))
        {
            return (request, CreateOAuthError("unauthorized_client", "response_type is not allowed for this client."), false);
        }

        var requestedScopes = ParseScopes(request.Scope);
        if (requestedScopes.Any(scope => !clientResult.Value.AllowedScopes.Contains(scope, StringComparer.Ordinal)))
        {
            return (request, CreateOAuthError("invalid_scope", "requested scope is not allowed for this client."), false);
        }

        string? resolvedRedirectUri;
        if (!string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            if (!Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
            {
                resolvedRedirectUri = null;
            }
            else
            {
                resolvedRedirectUri = clientResult.Value.RedirectUris.Contains(request.RedirectUri, StringComparer.Ordinal)
                    ? request.RedirectUri
                    : null;
            }
        }
        else
        {
            resolvedRedirectUri = clientResult.Value.RedirectUris.Count == 1
                ? clientResult.Value.RedirectUris[0]
                : null;
        }

        if (resolvedRedirectUri is null)
        {
            var errorDescription = string.IsNullOrWhiteSpace(request.RedirectUri)
                ? "redirect_uri is required when the client does not have exactly one registered redirect URI."
                : "redirect_uri is not registered for this client.";
            return (request, CreateOAuthError("invalid_request", errorDescription), false);
        }

        var resolvedRequest = request with { RedirectUri = resolvedRedirectUri };

        string? pkceValidation = null;
        if (!string.IsNullOrWhiteSpace(resolvedRequest.CodeChallenge))
        {
            if (resolvedRequest.CodeChallenge.Length is < 43 or > 128)
            {
                pkceValidation = "code_challenge length is invalid.";
            }
            else
            {
                var normalizedMethod = string.IsNullOrWhiteSpace(resolvedRequest.CodeChallengeMethod)
                    ? "plain"
                    : resolvedRequest.CodeChallengeMethod.Trim();
                if (!string.Equals(normalizedMethod, "plain", StringComparison.Ordinal)
                    && !string.Equals(normalizedMethod, "S256", StringComparison.Ordinal))
                {
                    pkceValidation = "code_challenge_method is not supported.";
                }
            }
        }

        return pkceValidation is null
            ? (resolvedRequest, null, true)
            : (resolvedRequest, CreateOAuthError("invalid_request", pkceValidation), true);
    }

    private static bool RedirectUriMatches(string requestedRedirectUri, string expectedRedirectUri, bool wasRedirectUriSupplied)
        => wasRedirectUriSupplied
            ? FixedTimeEquals(expectedRedirectUri, requestedRedirectUri)
            : string.IsNullOrWhiteSpace(requestedRedirectUri) || FixedTimeEquals(expectedRedirectUri, requestedRedirectUri);

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

    internal static IReadOnlySet<string> ParseScopes(string scope)
        => scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    internal static string NormalizeScope(string scope)
        => string.Join(' ', ParseScopes(scope).OrderBy(static value => value, StringComparer.Ordinal));

    internal static bool ShouldIssueRefreshToken(string scope)
        => ParseScopes(scope).Contains(OAuthScope.OfflineAccess);

    internal static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    internal static bool ShouldEmitIdentityClaim(string claimType, IReadOnlySet<string> scopes)
    {
        return claimType switch
        {
            OAuthClaimTypes.Email or OAuthClaimTypes.EmailVerified => scopes.Contains(OAuthScope.Email),
            OAuthClaimTypes.Name or OAuthClaimTypes.Groups => scopes.Contains(OAuthScope.Profile),
            OAuthClaimTypes.Scope or OAuthClaimTypes.SessionId => false,
            _ => true
        };
    }

    internal static OAuthError CreateOAuthError(string error, string description)
        => new()
        {
            Error = error,
            ErrorDescription = description
        };

    internal static AspNetResult CreateOAuthErrorResult(string error, string? description, int statusCode = StatusCodes.Status400BadRequest)
        => Results.Json(
            new OAuthError
            {
                Error = error,
                ErrorDescription = description ?? string.Empty
            },
            statusCode: statusCode);

    internal static Result<OAuthTokenEndpointResponse> Fail(string code, string message)
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

    internal static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    internal static void ApplyResolvedClientCredentials(HttpRequest httpRequest, OAuthTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        ArgumentNullException.ThrowIfNull(request);

        var clientCredentials = ResolveClientCredentials(httpRequest, request);
        if (clientCredentials.Error is not null)
        {
            return;
        }

        request.ClientId = clientCredentials.ClientId;
        request.ClientSecret = clientCredentials.ClientSecret ?? string.Empty;
    }

    internal static async Task<OAuthError?> ValidateClientAuthenticationAsync(
        HttpRequest httpRequest,
        OAuthTokenRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        string? clientAssertionAudience = null)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var clientRepository = ResolveService<IOAuthClientRepository>(serviceProvider);
        if (clientRepository is null)
        {
            return null;
        }

        var clientCredentials = ResolveClientCredentials(httpRequest, request);
        if (clientCredentials.Error is not null)
        {
            return clientCredentials.Error;
        }

        if (string.IsNullOrWhiteSpace(clientCredentials.ClientId))
        {
            return CreateOAuthError("invalid_client", "client_id is required.");
        }

        var clientResult = await clientRepository.GetAsync(clientCredentials.ClientId, cancellationToken).ConfigureAwait(false);
        if (!clientResult.IsSuccess || !clientResult.Value.IsEnabled)
        {
            return CreateOAuthError("invalid_client", "client credentials are invalid.");
        }

        if (!string.IsNullOrWhiteSpace(request.GrantType)
            && clientResult.Value.AllowedGrantTypes.Count > 0
            && !clientResult.Value.AllowedGrantTypes.Contains(request.GrantType, StringComparer.Ordinal))
        {
            return CreateOAuthError("unauthorized_client", "grant_type is not allowed for this client.");
        }

        if (clientResult.Value.ClientType == OAuthClientType.Public)
        {
            if (!string.Equals(
                    clientCredentials.AuthenticationMethod,
                    OAuthClientAuthenticationMethods.None,
                    StringComparison.Ordinal))
            {
                return CreateOAuthError("invalid_client", "public clients must use token_endpoint_auth_method=none.");
            }

            if (string.Equals(
                    clientCredentials.AuthenticationMethod,
                    OAuthClientAuthenticationMethods.PrivateKeyJwt,
                    StringComparison.Ordinal))
            {
                return CreateOAuthError("invalid_client", "public clients must not use client assertions.");
            }

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

            if (string.Equals(request.GrantType, OAuthGrantTypes.RefreshToken, StringComparison.Ordinal)
                || string.Equals(request.GrantType, OAuthGrantTypes.TokenExchange, StringComparison.Ordinal)
                || string.Equals(request.GrantType, OAuthGrantTypes.DeviceCode, StringComparison.Ordinal))
            {
                return null;
            }

            return CreateOAuthError("invalid_client", "public client authentication is invalid for this grant type.");
        }

        if (!string.Equals(
                clientCredentials.AuthenticationMethod,
                OAuthClientAuthenticationMethods.PrivateKeyJwt,
                StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(clientCredentials.ClientSecret))
        {
            return CreateOAuthError("invalid_client", "client_secret is required.");
        }

        var validation = await clientRepository.ValidateClientCredentialsAsync(
            new OAuthClientCredentialsValidationRequest(
                clientCredentials.ClientId,
                clientCredentials.ClientSecret,
                ParseScopes(request.Scope),
                clientCredentials.AuthenticationMethod,
                EmptyToNull(request.ClientAssertion),
                EmptyToNull(request.ClientAssertionType),
                clientAssertionAudience ?? ResolveTokenEndpointAudience(serviceProvider),
                request.GrantType),
            cancellationToken).ConfigureAwait(false);

        return validation switch
        {
            OAuthClientCredentialsValidationResult.Success => null,
            OAuthClientCredentialsValidationResult.Failure failure => CreateOAuthError(failure.Error, failure.ErrorDescription),
            _ => CreateOAuthError("invalid_client", "client credentials are invalid.")
        };
    }

    internal static async Task<(OAuthError? Error, string ClientId)> AuthenticateClientAsync(
        HttpRequest httpRequest,
        string? clientId,
        string? clientSecret,
        string scope,
        IServiceProvider serviceProvider,
        bool allowPublicClient,
        CancellationToken cancellationToken)
    {
        var clientRepository = ResolveService<IOAuthClientRepository>(serviceProvider);
        if (clientRepository is null)
        {
            return (CreateOAuthError("server_error", "OAuth client repository is not registered."), string.Empty);
        }

        var request = new OAuthTokenRequest
        {
            ClientId = clientId ?? string.Empty,
            ClientSecret = clientSecret ?? string.Empty,
            Scope = scope
        };
        var clientCredentials = ResolveClientCredentials(httpRequest, request);
        if (clientCredentials.Error is not null)
        {
            return (clientCredentials.Error, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(clientCredentials.ClientId))
        {
            return (CreateOAuthError("invalid_client", "client_id is required."), string.Empty);
        }

        var clientResult = await clientRepository.GetAsync(clientCredentials.ClientId, cancellationToken).ConfigureAwait(false);
        if (!clientResult.IsSuccess || !clientResult.Value.IsEnabled)
        {
            return (CreateOAuthError("invalid_client", "client credentials are invalid."), string.Empty);
        }

        if (clientResult.Value.ClientType == OAuthClientType.Public)
        {
            if (!allowPublicClient)
            {
                return (CreateOAuthError("invalid_client", "public client authentication is invalid for this endpoint."), string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(clientCredentials.ClientSecret))
            {
                return (CreateOAuthError("invalid_client", "public clients must not use client_secret."), string.Empty);
            }

            if (!string.Equals(
                    clientCredentials.AuthenticationMethod,
                    OAuthClientAuthenticationMethods.None,
                    StringComparison.Ordinal))
            {
                return (CreateOAuthError("invalid_client", "public clients must use token_endpoint_auth_method=none."), string.Empty);
            }

            return (null, clientCredentials.ClientId);
        }

        if (string.IsNullOrWhiteSpace(clientCredentials.ClientSecret))
        {
            return (CreateOAuthError("invalid_client", "client_secret is required."), string.Empty);
        }

        var validation = await clientRepository.ValidateClientCredentialsAsync(
            new OAuthClientCredentialsValidationRequest(
                clientCredentials.ClientId,
                clientCredentials.ClientSecret,
                ParseScopes(scope),
                clientCredentials.AuthenticationMethod),
            cancellationToken).ConfigureAwait(false);

        return validation switch
        {
            OAuthClientCredentialsValidationResult.Success => (null, clientCredentials.ClientId),
            OAuthClientCredentialsValidationResult.Failure failure =>
                (CreateOAuthError(failure.Error, failure.ErrorDescription), string.Empty),
            _ => (CreateOAuthError("invalid_client", "client credentials are invalid."), string.Empty)
        };
    }

    private static (string ClientId, string? ClientSecret, string AuthenticationMethod, OAuthError? Error) ResolveClientCredentials(
        HttpRequest httpRequest,
        OAuthTokenRequest request)
    {
        const string basicAuthenticationPrefix = "Basic ";
        var authorization = httpRequest.Headers.Authorization.ToString();
        var hasBasicAuthentication = !string.IsNullOrWhiteSpace(authorization)
            && authorization.StartsWith(basicAuthenticationPrefix, StringComparison.OrdinalIgnoreCase);
        var hasClientAssertion = !string.IsNullOrWhiteSpace(request.ClientAssertion)
            || !string.IsNullOrWhiteSpace(request.ClientAssertionType);
        if (hasClientAssertion)
        {
            if (hasBasicAuthentication || !string.IsNullOrWhiteSpace(request.ClientSecret))
            {
                return (
                    request.ClientId,
                    null,
                    OAuthClientAuthenticationMethods.PrivateKeyJwt,
                    CreateOAuthError("invalid_client", "multiple client authentication methods are not allowed."));
            }

            var assertionClientId = request.ClientId;
            if (string.IsNullOrWhiteSpace(assertionClientId)
                && !string.IsNullOrWhiteSpace(request.ClientAssertion))
            {
                try
                {
                    assertionClientId = new JwtSecurityTokenHandler()
                        .ReadJwtToken(request.ClientAssertion)
                        .Issuer;
                }
                catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
                {
                    assertionClientId = string.Empty;
                }
            }

            return (
                assertionClientId,
                null,
                OAuthClientAuthenticationMethods.PrivateKeyJwt,
                null);
        }

        if (hasBasicAuthentication)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[basicAuthenticationPrefix.Length..].Trim()));
                var separatorIndex = decoded.IndexOf(':', StringComparison.Ordinal);
                if (separatorIndex >= 0)
                {
                    return (
                        Uri.UnescapeDataString(decoded[..separatorIndex]),
                        Uri.UnescapeDataString(decoded[(separatorIndex + 1)..]),
                        OAuthClientAuthenticationMethods.ClientSecretBasic,
                        null);
                }
            }
            catch (FormatException)
            {
                // Ignore invalid Basic auth and fall back to form credentials.
            }
        }

        var formSecret = EmptyToNull(request.ClientSecret);
        return (
            request.ClientId,
            formSecret,
            formSecret is null
                ? OAuthClientAuthenticationMethods.None
                : OAuthClientAuthenticationMethods.ClientSecretPost,
            null);
    }

    private static string BuildTokenEndpointAudience(OAuthAuthorizationServerOptions options)
        => $"{options.Issuer.TrimEnd('/')}/token";

    private static string? ResolveTokenEndpointAudience(IServiceProvider serviceProvider)
    {
        var options = ResolveService<IOptions<OAuthAuthorizationServerOptions>>(serviceProvider)?.Value;
        return options is null || string.IsNullOrWhiteSpace(options.Issuer)
            ? null
            : BuildTokenEndpointAudience(options);
    }

    internal static OAuthIntrospectionResponse BuildIntrospectionResponse(IntrospectTokenResponse response)
    {
        if (!response.Active)
        {
            return new OAuthIntrospectionResponse
            {
                Active = false
            };
        }

        var audiences = ExpandTokenClaims(response.Claims)
            .Where(claim => string.Equals(claim.Type, JwtRegisteredClaimNames.Aud, StringComparison.Ordinal))
            .Select(static claim => claim.Value ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var additionalClaims = ExpandTokenClaims(response.Claims)
            .Where(static claim =>
                !string.Equals(claim.Type, OAuthClaimTypes.Scope, StringComparison.Ordinal)
                && !string.Equals(claim.Type, OAuthClaimTypes.ClientId, StringComparison.Ordinal)
                && !string.Equals(claim.Type, OAuthClaimTypes.Subject, StringComparison.Ordinal)
                && !string.Equals(claim.Type, JwtRegisteredClaimNames.Exp, StringComparison.Ordinal)
                && !string.Equals(claim.Type, JwtRegisteredClaimNames.Iat, StringComparison.Ordinal)
                && !string.Equals(claim.Type, JwtRegisteredClaimNames.Nbf, StringComparison.Ordinal)
                && !string.Equals(claim.Type, JwtRegisteredClaimNames.Iss, StringComparison.Ordinal)
                && !string.Equals(claim.Type, JwtRegisteredClaimNames.Aud, StringComparison.Ordinal)
                && !string.Equals(claim.Type, JwtRegisteredClaimNames.Jti, StringComparison.Ordinal))
            .GroupBy(static claim => claim.Type, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Count() == 1
                    ? JsonSerializer.SerializeToElement(ToJsonValue(group.First()))
                    : JsonSerializer.SerializeToElement(group.Select(ToJsonValue).ToArray()),
                StringComparer.Ordinal);

        return new OAuthIntrospectionResponse
        {
            Active = true,
            Scope = GetSingleClaimValue(response.Claims, OAuthClaimTypes.Scope),
            ClientId = GetSingleClaimValue(response.Claims, OAuthClaimTypes.ClientId),
            TokenType = response.TokenType,
            Subject = GetSingleClaimValue(response.Claims, OAuthClaimTypes.Subject),
            ExpiresAt = GetNumericClaimValue(response.Claims, JwtRegisteredClaimNames.Exp),
            IssuedAt = GetNumericClaimValue(response.Claims, JwtRegisteredClaimNames.Iat),
            NotBefore = GetNumericClaimValue(response.Claims, JwtRegisteredClaimNames.Nbf),
            Issuer = GetSingleClaimValue(response.Claims, JwtRegisteredClaimNames.Iss),
            Audience = audiences.Length == 0 ? null : audiences,
            TokenId = GetSingleClaimValue(response.Claims, JwtRegisteredClaimNames.Jti),
            AdditionalClaims = additionalClaims.Count == 0 ? null : additionalClaims
        };
    }

    private static object ToJsonValue(TokenClaim claim)
    {
        if (string.Equals(claim.ValueType, IdentityModel.JsonWebTokens.JsonClaimValueTypes.Json, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(claim.Value))
        {
            return JsonDocument.Parse(claim.Value).RootElement.Clone();
        }

        if (string.Equals(claim.ValueType, ClaimValueTypes.Integer64, StringComparison.Ordinal)
            && long.TryParse(claim.Value, out var longValue))
        {
            return longValue;
        }

        if (string.Equals(claim.ValueType, ClaimValueTypes.Boolean, StringComparison.Ordinal)
            && bool.TryParse(claim.Value, out var booleanValue))
        {
            return booleanValue;
        }

        return claim.Value ?? string.Empty;
    }

    private static string? GetSingleClaimValue(IEnumerable<TokenClaim> claims, string type)
        => ExpandTokenClaims(claims)
            .FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            ?.Value;

    private static long? GetNumericClaimValue(IEnumerable<TokenClaim> claims, string type)
    {
        var value = GetSingleClaimValue(claims, type);
        return long.TryParse(value, out var result) ? result : null;
    }

    private static IEnumerable<TokenClaim> ExpandTokenClaims(IEnumerable<TokenClaim> claims)
    {
        foreach (var claim in claims)
        {
            if (claim.Values is { Length: > 0 })
            {
                foreach (var value in claim.Values)
                {
                    yield return new TokenClaim(claim.Type, value, claim.ValueType);
                }

                continue;
            }

            if (claim.Value is null)
            {
                continue;
            }

            yield return claim;
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

    internal static async ValueTask<string> ResolveBearerTokenAsync(HttpRequest request, CancellationToken cancellationToken)
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

    private sealed record OidcRedeemedAuthorizationCodeCacheKey(string Code)
        : CacheKey<OidcRedeemedAuthorizationCodeCacheValue>($"nof:oauth:auth-code:redeemed:{Code}");

    private sealed record OidcRedeemedAuthorizationCodeCacheValue
    {
        public required string ClientId { get; init; }

        public required string RedirectUri { get; init; }

        public required bool WasRedirectUriSupplied { get; init; }

        public required OAuthTokenEndpointResponse Response { get; init; }
    }
}
