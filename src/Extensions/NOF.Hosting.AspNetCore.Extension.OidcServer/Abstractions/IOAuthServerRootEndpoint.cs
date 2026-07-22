using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthServerRootEndpoint
{
    ValueTask<IResult> HandleAsync(CancellationToken cancellationToken);
}
