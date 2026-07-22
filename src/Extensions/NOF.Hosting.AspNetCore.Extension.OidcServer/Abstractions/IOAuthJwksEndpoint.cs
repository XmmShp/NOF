using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthJwksEndpoint
{
    Task<IResult> HandleAsync(CancellationToken cancellationToken);
}
