using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed record OAuthServerRootRequest;

public sealed record OAuthServerMetadataRequest;

public sealed record OAuthJwksRequest;

public sealed record OAuthAuthorizeRequest
{
    public string ResponseType { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string? Nonce { get; set; }

    public string? CodeChallenge { get; set; }

    public string? CodeChallengeMethod { get; set; }
}

public sealed record OAuthTokenRequest
{
    public string GrantType { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string CodeVerifier { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;
}

public sealed record OAuthUserInfoRequest
{
    [FromHeader("Authorization", Prefix = "Bearer")]
    public string AccessToken { get; set; } = string.Empty;
}
