using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthRevokeEndpoint
{
    Task<IResult> HandleAsync(OAuthRevokeEndpointRequest request, CancellationToken cancellationToken);
}
