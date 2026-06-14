namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthClientStore
{
    ValueTask<OAuthClientCredentialsValidationResult> ValidateClientCredentialsAsync(
        OAuthClientCredentialsValidationRequest request,
        CancellationToken cancellationToken);
}
