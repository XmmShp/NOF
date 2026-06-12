namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthAuthorizationCodeService
{
    ValueTask<string> CreateAsync(
        OAuthAuthorizationCodeDescriptor descriptor,
        CancellationToken cancellationToken);
}
