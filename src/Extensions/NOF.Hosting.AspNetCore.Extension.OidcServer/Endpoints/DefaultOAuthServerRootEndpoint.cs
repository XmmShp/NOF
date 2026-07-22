using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class DefaultOAuthServerRootEndpoint(IOptions<OAuthAuthorizationServerOptions> options) : IOAuthServerRootEndpoint
{
    public ValueTask<IResult> HandleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issuer = options.Value.Issuer.TrimEnd('/');
        return ValueTask.FromResult<IResult>(Results.Json(new OAuthServerRootDocument
        {
            Issuer = issuer,
            Metadata = OAuthAuthorizationServerMetadataUris.BuildMetadataEndpoint(issuer, requireHttps: false).ToString()
        }));
    }
}
