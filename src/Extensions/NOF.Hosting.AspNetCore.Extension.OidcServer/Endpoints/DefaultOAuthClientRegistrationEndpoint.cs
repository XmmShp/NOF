using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Contract;
using System.Text.Json;
using AspNetResult = Microsoft.AspNetCore.Http.IResult;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthClientRegistrationEndpoint(
    IOAuthInitialAccessTokenHandler initialAccessTokenHandler,
    IOAuthClientRegistrationRepository registrationRepository,
    IOptions<OAuthAuthorizationServerOptions> options) : IOAuthClientRegistrationEndpoint
{
    private readonly OAuthAuthorizationServerOptions _options = options.Value;

    public async Task<AspNetResult> RegisterAsync(
        HttpRequest httpRequest,
        OAuthClientRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsTransportAllowed(httpRequest))
        {
            return CreateError(httpRequest, "invalid_request", "dynamic client registration requires HTTPS.");
        }

        var authorization = await initialAccessTokenHandler
            .HandleAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        if (!authorization.IsSuccess)
        {
            var statusCode = string.Equals(authorization.ErrorCode, "insufficient_scope", StringComparison.Ordinal)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized;
            return CreateError(httpRequest, authorization.ErrorCode, authorization.Message, statusCode);
        }

        var normalized = Normalize(request);
        if (!normalized.IsSuccess)
        {
            return CreateError(httpRequest, normalized.ErrorCode, normalized.Message);
        }

        var result = await registrationRepository
            .RegisterAsync(normalized.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return CreateRepositoryError(httpRequest, result.ErrorCode, result.Message);
        }

        SetNoStore(httpRequest);
        return Results.Json(
            CreateResponse(result.Value),
            statusCode: StatusCodes.Status201Created);
    }

    public async Task<AspNetResult> GetAsync(
        HttpRequest httpRequest,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (!IsTransportAllowed(httpRequest))
        {
            return CreateError(httpRequest, "invalid_request", "client configuration requires HTTPS.");
        }

        if (!DefaultOAuthInitialAccessTokenHandler.TryReadBearerToken(httpRequest, out var registrationAccessToken))
        {
            return CreateError(httpRequest, "invalid_token", "registration access token is required.", StatusCodes.Status401Unauthorized);
        }

        var result = await registrationRepository
            .GetAsync(clientId, registrationAccessToken, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return CreateRepositoryError(httpRequest, result.ErrorCode, result.Message);
        }

        SetNoStore(httpRequest);
        return Results.Json(CreateResponse(result.Value));
    }

    public async Task<AspNetResult> UpdateAsync(
        HttpRequest httpRequest,
        string clientId,
        OAuthClientRegistrationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsTransportAllowed(httpRequest))
        {
            return CreateError(httpRequest, "invalid_request", "client configuration requires HTTPS.");
        }

        if (!DefaultOAuthInitialAccessTokenHandler.TryReadBearerToken(httpRequest, out var registrationAccessToken))
        {
            return CreateError(httpRequest, "invalid_token", "registration access token is required.", StatusCodes.Status401Unauthorized);
        }

        if (!string.Equals(clientId, request.ClientId, StringComparison.Ordinal))
        {
            return CreateError(httpRequest, "invalid_client_metadata", "client_id must match the client configuration endpoint.");
        }

        var normalized = Normalize(request);
        if (!normalized.IsSuccess)
        {
            return CreateError(httpRequest, normalized.ErrorCode, normalized.Message);
        }

        var createRequest = normalized.Value;
        var updateRequest = new UpdateOAuthClientRequest
        {
            DisplayName = createRequest.DisplayName,
            AllowedScopes = createRequest.AllowedScopes,
            RedirectUris = createRequest.RedirectUris,
            AccessTokenClaims = [],
            JsonWebKeySet = createRequest.JsonWebKeySet ?? string.Empty,
            TokenEndpointAuthenticationMethod = createRequest.TokenEndpointAuthenticationMethod,
            AllowedGrantTypes = createRequest.AllowedGrantTypes,
            AllowedResponseTypes = createRequest.AllowedResponseTypes,
            ApplicationType = createRequest.ApplicationType,
            SubjectType = createRequest.SubjectType,
            IdTokenSignedResponseAlgorithm = createRequest.IdTokenSignedResponseAlgorithm,
            ClientUri = createRequest.ClientUri,
            LogoUri = createRequest.LogoUri,
            Contacts = createRequest.Contacts,
            TermsOfServiceUri = createRequest.TermsOfServiceUri,
            PolicyUri = createRequest.PolicyUri,
            SoftwareId = createRequest.SoftwareId,
            SoftwareVersion = createRequest.SoftwareVersion,
            ClientType = createRequest.ClientType,
            IsEnabled = true
        };
        var result = await registrationRepository.UpdateAsync(
            clientId,
            registrationAccessToken,
            request.ClientSecret,
            updateRequest,
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return CreateRepositoryError(httpRequest, result.ErrorCode, result.Message);
        }

        SetNoStore(httpRequest);
        return Results.Json(CreateResponse(result.Value));
    }

    public async Task<AspNetResult> DeleteAsync(
        HttpRequest httpRequest,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (!IsTransportAllowed(httpRequest))
        {
            return CreateError(httpRequest, "invalid_request", "client configuration requires HTTPS.");
        }

        if (!DefaultOAuthInitialAccessTokenHandler.TryReadBearerToken(httpRequest, out var registrationAccessToken))
        {
            return CreateError(httpRequest, "invalid_token", "registration access token is required.", StatusCodes.Status401Unauthorized);
        }

        var result = await registrationRepository
            .DeleteAsync(clientId, registrationAccessToken, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return CreateRepositoryError(httpRequest, result.ErrorCode, result.Message);
        }

        SetNoStore(httpRequest);
        return Results.NoContent();
    }

    private Result<CreateOAuthClientRequest> Normalize(OAuthClientRegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authenticationMethod = string.IsNullOrWhiteSpace(request.TokenEndpointAuthenticationMethod)
            ? OAuthClientAuthenticationMethods.ClientSecretBasic
            : request.TokenEndpointAuthenticationMethod.Trim();
        if (!_options.ClientRegistrationAuthenticationMethodsAllowed.Contains(authenticationMethod, StringComparer.Ordinal))
        {
            return Result.Fail("invalid_client_metadata", "token_endpoint_auth_method is not allowed.");
        }

        var clientType = string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.None, StringComparison.Ordinal)
            ? OAuthClientType.Public
            : OAuthClientType.Confidential;
        var grantTypes = NormalizeValues(request.GrantTypes ?? [OAuthGrantTypes.AuthorizationCode]);
        var responseTypes = NormalizeValues(request.ResponseTypes ?? ["code"]);
        var redirectUris = NormalizeValues(request.RedirectUris ?? []);
        var scopes = (request.Scope ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        if (grantTypes.Any(grantType => !_options.ClientRegistrationGrantTypesAllowed.Contains(grantType, StringComparer.Ordinal)))
        {
            return Result.Fail("invalid_client_metadata", "grant_types contains a value that is not allowed.");
        }

        if (responseTypes.Any(static responseType => !string.Equals(responseType, "code", StringComparison.Ordinal)))
        {
            return Result.Fail("invalid_client_metadata", "only response_type=code is supported.");
        }

        if (grantTypes.Contains(OAuthGrantTypes.AuthorizationCode, StringComparer.Ordinal)
            != responseTypes.Contains("code", StringComparer.Ordinal))
        {
            return Result.Fail("invalid_client_metadata", "authorization_code and response_type=code must be registered together.");
        }

        if (clientType == OAuthClientType.Public
            && grantTypes.Contains(OAuthGrantTypes.ClientCredentials, StringComparer.Ordinal))
        {
            return Result.Fail("invalid_client_metadata", "public clients must not use the client_credentials grant type.");
        }

        if (scopes.Any(scope => !_options.ClientRegistrationScopesAllowed.Contains(scope, StringComparer.Ordinal)))
        {
            return Result.Fail("invalid_client_metadata", "scope contains a value that is not allowed.");
        }

        var applicationType = string.IsNullOrWhiteSpace(request.ApplicationType)
            ? OAuthClientApplicationTypes.Web
            : request.ApplicationType.Trim();
        if (!string.Equals(applicationType, OAuthClientApplicationTypes.Web, StringComparison.Ordinal)
            && !string.Equals(applicationType, OAuthClientApplicationTypes.Native, StringComparison.Ordinal))
        {
            return Result.Fail("invalid_client_metadata", "application_type must be web or native.");
        }

        var redirectError = ValidateRedirectUris(redirectUris, applicationType);
        if (redirectError is not null)
        {
            return Result.Fail("invalid_redirect_uri", redirectError);
        }

        if (grantTypes.Contains(OAuthGrantTypes.AuthorizationCode, StringComparer.Ordinal) && redirectUris.Length == 0)
        {
            return Result.Fail("invalid_redirect_uri", "redirect_uris is required for authorization_code clients.");
        }

        var subjectType = string.IsNullOrWhiteSpace(request.SubjectType)
            ? OAuthSubjectTypes.Public
            : request.SubjectType.Trim();
        if (!string.Equals(subjectType, OAuthSubjectTypes.Public, StringComparison.Ordinal))
        {
            return Result.Fail("invalid_client_metadata", "only subject_type=public is supported.");
        }

        var idTokenAlgorithm = string.IsNullOrWhiteSpace(request.IdTokenSignedResponseAlgorithm)
            ? OAuthSigningAlgorithms.RsaSha256
            : request.IdTokenSignedResponseAlgorithm.Trim();
        if (!string.Equals(idTokenAlgorithm, OAuthSigningAlgorithms.RsaSha256, StringComparison.Ordinal))
        {
            return Result.Fail("invalid_client_metadata", "only id_token_signed_response_alg=RS256 is supported.");
        }

        var jsonWebKeySet = request.JsonWebKeySet?.GetRawText();
        if (request.JsonWebKeySet.HasValue
            && request.JsonWebKeySet.Value.ValueKind != JsonValueKind.Object)
        {
            return Result.Fail("invalid_client_metadata", "jwks must be a JSON object.");
        }

        if (string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.PrivateKeyJwt, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(jsonWebKeySet))
        {
            return Result.Fail("invalid_client_metadata", "private_key_jwt clients must register jwks.");
        }

        if (clientType == OAuthClientType.Public && !string.IsNullOrWhiteSpace(jsonWebKeySet))
        {
            return Result.Fail("invalid_client_metadata", "public clients must not register client assertion keys.");
        }

        var metadataUriError = ValidateMetadataUris(request);
        if (metadataUriError is not null)
        {
            return Result.Fail("invalid_client_metadata", metadataUriError);
        }

        if ((request.ClientName?.Length ?? 0) > 256
            || (request.SoftwareId?.Length ?? 0) > 256
            || (request.SoftwareVersion?.Length ?? 0) > 256)
        {
            return Result.Fail("invalid_client_metadata", "client metadata exceeds the supported length.");
        }

        var contacts = NormalizeValues(request.Contacts ?? []);
        if (redirectUris.Length > 20 || scopes.Length > 64 || contacts.Length > 20)
        {
            return Result.Fail("invalid_client_metadata", "client metadata contains too many values.");
        }

        return Result.Success(new CreateOAuthClientRequest
        {
            DisplayName = request.ClientName?.Trim() ?? string.Empty,
            AllowedScopes = scopes,
            RedirectUris = redirectUris,
            AccessTokenClaims = [],
            JsonWebKeySet = jsonWebKeySet,
            TokenEndpointAuthenticationMethod = authenticationMethod,
            AllowedGrantTypes = grantTypes,
            AllowedResponseTypes = responseTypes,
            ApplicationType = applicationType,
            SubjectType = subjectType,
            IdTokenSignedResponseAlgorithm = idTokenAlgorithm,
            ClientUri = EmptyToNull(request.ClientUri),
            LogoUri = EmptyToNull(request.LogoUri),
            Contacts = contacts,
            TermsOfServiceUri = EmptyToNull(request.TermsOfServiceUri),
            PolicyUri = EmptyToNull(request.PolicyUri),
            SoftwareId = EmptyToNull(request.SoftwareId),
            SoftwareVersion = EmptyToNull(request.SoftwareVersion),
            ClientType = clientType,
            IsEnabled = true
        });
    }

    private OAuthClientRegistrationResponse CreateResponse(OAuthClientRegistrationSecretDescriptor result)
    {
        JsonElement? jsonWebKeySet = null;
        if (!string.IsNullOrWhiteSpace(result.Client.JsonWebKeySet))
        {
            jsonWebKeySet = JsonSerializer.Deserialize<JsonElement>(result.Client.JsonWebKeySet);
        }

        var issuer = _options.Issuer.TrimEnd('/');
        return new OAuthClientRegistrationResponse
        {
            RegistrationAccessToken = result.RegistrationAccessToken,
            RegistrationClientUri = $"{issuer}/register/{Uri.EscapeDataString(result.Client.ClientId)}",
            ClientId = result.Client.ClientId,
            ClientSecret = result.ClientSecret,
            ClientIdIssuedAt = new DateTimeOffset(result.Client.CreatedAtUtc).ToUnixTimeSeconds(),
            ClientSecretExpiresAt = result.ClientSecret is null ? null : 0,
            RedirectUris = result.Client.RedirectUris,
            TokenEndpointAuthenticationMethod = result.Client.TokenEndpointAuthenticationMethod,
            GrantTypes = result.Client.AllowedGrantTypes,
            ResponseTypes = result.Client.AllowedResponseTypes,
            ClientName = result.Client.DisplayName,
            ClientUri = result.Client.ClientUri,
            LogoUri = result.Client.LogoUri,
            Scope = string.Join(' ', result.Client.AllowedScopes.OrderBy(static value => value, StringComparer.Ordinal)),
            Contacts = result.Client.Contacts,
            TermsOfServiceUri = result.Client.TermsOfServiceUri,
            PolicyUri = result.Client.PolicyUri,
            JsonWebKeySet = jsonWebKeySet,
            SoftwareId = result.Client.SoftwareId,
            SoftwareVersion = result.Client.SoftwareVersion,
            ApplicationType = result.Client.ApplicationType,
            SubjectType = result.Client.SubjectType,
            IdTokenSignedResponseAlgorithm = result.Client.IdTokenSignedResponseAlgorithm
        };
    }

    private static string? ValidateRedirectUris(IReadOnlyList<string> redirectUris, string applicationType)
    {
        foreach (var redirectUri in redirectUris)
        {
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment))
            {
                return "redirect_uris must contain absolute URIs without fragments.";
            }

            if (string.Equals(applicationType, OAuthClientApplicationTypes.Web, StringComparison.Ordinal))
            {
                if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return "web client redirect_uris must use HTTPS.";
                }
            }
            else if (!((string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback)
                       || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                       && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return "native client redirect_uris must use a loopback HTTP URI or a custom URI scheme.";
            }
        }

        return null;
    }

    private static string? ValidateMetadataUris(OAuthClientRegistrationRequest request)
    {
        var uris = new[]
        {
            (Name: "client_uri", Value: request.ClientUri),
            (Name: "logo_uri", Value: request.LogoUri),
            (Name: "tos_uri", Value: request.TermsOfServiceUri),
            (Name: "policy_uri", Value: request.PolicyUri)
        };
        foreach (var (name, value) in uris)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.Length > 2048
                || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return $"{name} must be an absolute HTTPS URI no longer than 2048 characters.";
            }
        }

        return null;
    }

    private static string[] NormalizeValues(IEnumerable<string> values)
        => values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool IsTransportAllowed(HttpRequest request)
        => !_options.RequireHttpsForClientRegistration || request.IsHttps;

    private static AspNetResult CreateRepositoryError(HttpRequest request, string errorCode, string message)
    {
        if (string.Equals(errorCode, "invalid_token", StringComparison.Ordinal))
        {
            return CreateError(request, errorCode, message, StatusCodes.Status401Unauthorized);
        }

        if (string.Equals(errorCode, "invalid_redirect_uri", StringComparison.Ordinal)
            || string.Equals(errorCode, "invalid_client_metadata", StringComparison.Ordinal))
        {
            return CreateError(request, errorCode, message);
        }

        return CreateError(request, "server_error", message, StatusCodes.Status500InternalServerError);
    }

    private static AspNetResult CreateError(HttpRequest request, string error, string description, int statusCode = StatusCodes.Status400BadRequest)
    {
        SetNoStore(request);
        if (statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            request.HttpContext.Response.Headers.WWWAuthenticate = $"Bearer error=\"{error}\"";
        }

        return Results.Json(
            new OAuthError
            {
                Error = error,
                ErrorDescription = description
            },
            statusCode: statusCode);
    }

    private static void SetNoStore(HttpRequest request)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        request.HttpContext.Response.Headers.Pragma = "no-cache";
    }
}
