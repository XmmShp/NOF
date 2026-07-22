using Microsoft.AspNetCore.Http;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface IOAuthMetadataEndpoint
{
    ValueTask<IResult> HandleAsync(CancellationToken cancellationToken);
}
