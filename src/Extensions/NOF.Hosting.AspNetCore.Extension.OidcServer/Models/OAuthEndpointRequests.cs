using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed record OAuthAuthorizeEndpointRequest(
    OAuthAuthorizeRequest Request,
    bool WasRedirectUriSupplied);

public sealed record OAuthTokenEndpointRequest(
    HttpRequest HttpRequest,
    OAuthTokenRequest Request);

public sealed record OAuthRevokeEndpointRequest(
    HttpRequest HttpRequest,
    string? Token,
    string? TokenTypeHint,
    string? ClientId,
    string? ClientSecret);

public sealed record OAuthIntrospectEndpointRequest(
    HttpRequest HttpRequest,
    string? Token,
    string? TokenTypeHint,
    string? ClientId,
    string? ClientSecret);
