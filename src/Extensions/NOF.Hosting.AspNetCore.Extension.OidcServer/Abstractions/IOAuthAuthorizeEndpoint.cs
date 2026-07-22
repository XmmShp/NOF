using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthAuthorizeEndpoint
{
    Task<IResult> HandleAsync(OAuthAuthorizeEndpointRequest request, CancellationToken cancellationToken);
}
