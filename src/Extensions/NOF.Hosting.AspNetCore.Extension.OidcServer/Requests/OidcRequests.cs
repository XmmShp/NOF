namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

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
