using Microsoft.AspNetCore.Http;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthJwksEndpoint(IJwksService jwksService) : IOAuthJwksEndpoint
{
    public async Task<IResult> HandleAsync(CancellationToken cancellationToken)
        => Results.Json(await jwksService.GetJwksAsync(cancellationToken).ConfigureAwait(false));
}
