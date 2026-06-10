namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed record OAuthTokenRequest
{
    public string GrantType { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string CodeVerifier { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;
}
