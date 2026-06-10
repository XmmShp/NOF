using NOF.Contract;
using System.ComponentModel;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public interface IOAuthAuthorizationServerService : IRpcService
{
    [Summary("OAuth server root")]
    [Description("Returns the OAuth authorization server root document.")]
    [Category("OAuth Authorization Server")]
    [HttpEndpoint(HttpVerb.Get, "oauth2")]
    Result<OAuthServerRootDocument> GetRoot(Empty request);

    [Summary("OpenID Connect metadata")]
    [Description("Returns the OpenID Connect discovery metadata document.")]
    [Category("OAuth Authorization Server")]
    [HttpEndpoint(HttpVerb.Get, ".well-known/openid-configuration")]
    [HttpEndpoint(HttpVerb.Get, "oauth2/.well-known/openid-configuration")]
    Result<OAuthServerMetadata> GetOpenIdConfiguration(Empty request);

    [Summary("OAuth authorization server metadata")]
    [Description("Returns the OAuth authorization server metadata document.")]
    [Category("OAuth Authorization Server")]
    [HttpEndpoint(HttpVerb.Get, ".well-known/oauth-authorization-server")]
    [HttpEndpoint(HttpVerb.Get, "oauth2/.well-known/oauth-authorization-server")]
    Result<OAuthServerMetadata> GetAuthorizationServerMetadata(Empty request);

    [Summary("JSON Web Key Set")]
    [Description("Returns public signing keys as a JWKS document.")]
    [Category("OAuth Authorization Server")]
    [HttpEndpoint(HttpVerb.Get, ".well-known/jwks.json")]
    [HttpEndpoint(HttpVerb.Get, "oauth2/.well-known/jwks.json")]
    Result<JwksDocument> GetJwks(Empty request);

    [Summary("OAuth authorize")]
    [Description("Starts or completes an OAuth authorization-code request.")]
    [Category("OAuth Authorization Server")]
    [HttpEndpoint(HttpVerb.Get, "oauth2/authorize")]
    Result<OAuthAuthorizeResponse> Authorize(OAuthAuthorizeRequest request);

    [Summary("OAuth token")]
    [Description("Issues OAuth/OIDC tokens from an authorization code or refresh token.")]
    [Category("OAuth Authorization Server")]
    [HttpEndpoint(HttpVerb.Post, "oauth2/token")]
    Result<OAuthTokenEndpointResponse> Token(OAuthTokenRequest request);

    [Summary("OpenID Connect userinfo")]
    [Description("Returns OpenID Connect claims for an access token.")]
    [Category("OAuth Authorization Server")]
    [HttpEndpoint(HttpVerb.Get, "oauth2/userinfo")]
    [HttpEndpoint(HttpVerb.Post, "oauth2/userinfo")]
    Result<IReadOnlyDictionary<string, object>> UserInfo(OAuthUserInfoRequest request);
}

public sealed record OAuthAuthorizeResponse
{
    public required OAuthAuthorizeResponseType Type { get; init; }

    public string? RedirectUrl { get; init; }

    public OAuthError? Error { get; init; }
}

public enum OAuthAuthorizeResponseType
{
    Redirect,
    Error
}
