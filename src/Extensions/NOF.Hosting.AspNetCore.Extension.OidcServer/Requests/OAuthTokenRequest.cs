using Microsoft.AspNetCore.Mvc;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed record OAuthTokenRequest
{
    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } = string.Empty;

    [FromForm(Name = "code")]
    public string Code { get; set; } = string.Empty;

    [FromForm(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    [FromForm(Name = "client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [FromForm(Name = "redirect_uri")]
    public string RedirectUri { get; set; } = string.Empty;

    [FromForm(Name = "code_verifier")]
    public string CodeVerifier { get; set; } = string.Empty;

    [FromForm(Name = "refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [FromForm(Name = "scope")]
    public string Scope { get; set; } = string.Empty;

    [FromForm(Name = "subject_token")]
    public string SubjectToken { get; set; } = string.Empty;

    [FromForm(Name = "subject_token_type")]
    public string SubjectTokenType { get; set; } = string.Empty;

    [FromForm(Name = "actor_token")]
    public string ActorToken { get; set; } = string.Empty;

    [FromForm(Name = "actor_token_type")]
    public string ActorTokenType { get; set; } = string.Empty;

    [FromForm(Name = "requested_token_type")]
    public string RequestedTokenType { get; set; } = string.Empty;
}
