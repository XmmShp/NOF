namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed record OAuthClientCredentialsValidationRequest(
    string ClientId,
    string? ClientSecret,
    IReadOnlySet<string> RequestedScopes,
    string AuthenticationMethod);

public abstract record OAuthClientCredentialsValidationResult
{
    public sealed record Success(
        string Subject,
        IReadOnlySet<string> Scopes,
        IReadOnlyList<KeyValuePair<string, string>> AccessTokenClaims) : OAuthClientCredentialsValidationResult;

    public sealed record Failure(string Error, string ErrorDescription) : OAuthClientCredentialsValidationResult;
}
