namespace NOF.Infrastructure.Extension.Authorization.Jwt;

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
