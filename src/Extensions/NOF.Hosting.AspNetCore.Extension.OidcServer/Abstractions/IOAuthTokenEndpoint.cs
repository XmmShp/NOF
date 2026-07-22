using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthTokenEndpoint
{
    Task<IResult> HandleAsync(OAuthTokenEndpointRequest request, CancellationToken cancellationToken);
}
