using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthDeviceAuthorizationEndpoint
{
    Task<IResult> HandleAsync(
        OAuthDeviceAuthorizationEndpointRequest request,
        CancellationToken cancellationToken);
}
