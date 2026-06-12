namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthAuthorizationHandler
{
    ValueTask<OAuthAuthorizationResult> AuthorizeAsync(
        OAuthAuthorizationRequest request,
        CancellationToken cancellationToken);
}
