using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthDeviceAuthorizationEndpoint(
    IServiceProvider serviceProvider,
    IOAuthClientRepository clientRepository,
    IOAuthDeviceGrantService deviceGrantService,
    IOptions<OAuthAuthorizationServerOptions> options) : IOAuthDeviceAuthorizationEndpoint
{
    public async Task<IResult> HandleAsync(
        OAuthDeviceAuthorizationEndpointRequest request,
        CancellationToken cancellationToken)
    {
        var httpRequest = request.HttpRequest;
        var serverOptions = options.Value;
        if (serverOptions.RequireHttpsForDeviceAuthorization && !httpRequest.IsHttps)
        {
            return CreateError(httpRequest, "invalid_request", "device authorization requires HTTPS.");
        }

        var tokenRequest = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.DeviceCode,
            ClientId = request.Request.ClientId,
            ClientSecret = request.Request.ClientSecret,
            ClientAssertionType = request.Request.ClientAssertionType,
            ClientAssertion = request.Request.ClientAssertion,
            Scope = OidcRoutes.NormalizeScope(request.Request.Scope)
        };
        OidcRoutes.ApplyResolvedClientCredentials(httpRequest, tokenRequest);
        var authenticationError = await OidcRoutes.ValidateClientAuthenticationAsync(
            httpRequest,
            tokenRequest,
            serviceProvider,
            cancellationToken,
            $"{serverOptions.Issuer.TrimEnd('/')}/device_authorization").ConfigureAwait(false);
        if (authenticationError is not null)
        {
            return CreateError(httpRequest, authenticationError.Error, authenticationError.ErrorDescription);
        }

        var clientResult = await clientRepository.GetAsync(tokenRequest.ClientId, cancellationToken).ConfigureAwait(false);
        if (!clientResult.IsSuccess || !clientResult.Value.IsEnabled)
        {
            return CreateError(httpRequest, "invalid_client", "client credentials are invalid.");
        }

        var requestedScopes = OidcRoutes.ParseScopes(tokenRequest.Scope);
        if (requestedScopes.Any(scope => !clientResult.Value.AllowedScopes.Contains(scope, StringComparer.Ordinal)))
        {
            return CreateError(httpRequest, "invalid_scope", "requested scope is not allowed for this client.");
        }

        var createResult = await deviceGrantService.CreateAsync(
            new CreateOAuthDeviceGrantRequest
            {
                ClientId = clientResult.Value.ClientId,
                ClientDisplayName = clientResult.Value.DisplayName,
                ClientLogoUri = clientResult.Value.LogoUri,
                Scopes = requestedScopes
            },
            cancellationToken).ConfigureAwait(false);
        if (!createResult.IsSuccess)
        {
            return CreateError(httpRequest, createResult.ErrorCode, createResult.Message);
        }

        SetNoStore(httpRequest);
        return Results.Json(createResult.Value);
    }

    private static IResult CreateError(HttpRequest request, string error, string description)
    {
        SetNoStore(request);
        return OidcRoutes.CreateOAuthErrorResult(error, description);
    }

    private static void SetNoStore(HttpRequest request)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        request.HttpContext.Response.Headers.Pragma = "no-cache";
    }
}
