using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthIntrospectEndpoint
{
    Task<IResult> HandleAsync(OAuthIntrospectEndpointRequest request, CancellationToken cancellationToken);
}
