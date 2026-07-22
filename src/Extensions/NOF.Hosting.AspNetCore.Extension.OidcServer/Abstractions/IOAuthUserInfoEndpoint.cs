using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthUserInfoEndpoint
{
    Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken);
}
