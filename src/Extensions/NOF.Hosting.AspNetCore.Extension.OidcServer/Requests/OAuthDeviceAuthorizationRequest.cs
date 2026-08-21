using Microsoft.AspNetCore.Mvc;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed record OAuthDeviceAuthorizationRequest
{
    [FromForm(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    [FromForm(Name = "client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [FromForm(Name = "client_assertion_type")]
    public string ClientAssertionType { get; set; } = string.Empty;

    [FromForm(Name = "client_assertion")]
    public string ClientAssertion { get; set; } = string.Empty;

    [FromForm(Name = "scope")]
    public string Scope { get; set; } = string.Empty;
}
