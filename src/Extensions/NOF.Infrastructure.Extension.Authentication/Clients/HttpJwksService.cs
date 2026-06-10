using Microsoft.Extensions.Options;
using NOF.Abstraction;
using System.Net.Http.Json;
using System.Text.Json;
using NOF.Contract.Extension.Authentication;

namespace NOF.Infrastructure.Extension.Authentication;

/// <summary>
/// Default HTTP-based JWKS client that fetches the well-known JWKS document directly.
/// </summary>
public sealed class HttpJwksService(HttpClient httpClient, IOptions<AuthenticationResourceServerOptions> options) : IJwksService
{
    public async Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync(options.Value.JwksEndpoint, JsonSerializerOptions.NOF.GetRequiredTypeInfo<JwksDocument>(), cancellationToken).ConfigureAwait(false)
            ?? new JwksDocument();
}
