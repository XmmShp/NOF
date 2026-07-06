namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthTokenExchangeHandler
{
    ValueTask<OAuthTokenExchangeResult> HandleAsync(
        OAuthTokenExchangeRequest request,
        CancellationToken cancellationToken);
}
