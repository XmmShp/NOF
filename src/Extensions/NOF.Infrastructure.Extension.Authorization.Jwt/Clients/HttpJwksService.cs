using Microsoft.Extensions.Options;
using NOF.Abstraction;
using System.Net.Http.Json;
using System.Text.Json;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Default HTTP-based JWKS client that fetches the well-known JWKS document directly.
/// </summary>
public sealed class HttpJwksService(HttpClient httpClient, IOptions<JwtResourceServerOptions> options) : IJwksService
{
    public async Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<JwksDocument>(options.Value.JwksEndpoint, JsonSerializerOptions.NOF, cancellationToken).ConfigureAwait(false)
            ?? new JwksDocument();
    }
}
