using Microsoft.AspNetCore.Http;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthDeviceVerificationEndpoint : IOAuthDeviceVerificationEndpoint
{
    public Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IResult>(OidcRoutes.CreateOAuthErrorResult(
            "server_error",
            "OAuth device verification endpoint is not configured. Replace IOAuthDeviceVerificationEndpoint to implement user interaction.",
            StatusCodes.Status501NotImplemented));
    }
}
