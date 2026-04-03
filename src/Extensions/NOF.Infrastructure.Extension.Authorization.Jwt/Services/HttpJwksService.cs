using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Default HTTP-based <see cref="IJwksService"/> implementation used by resource servers.
/// </summary>
public sealed class HttpJwksService(HttpClient httpClient, IOptions<JwtResourceServerOptions> options) : IJwksService
{
    private static readonly JsonTypeInfo<Result<JwksDocument>> ResultTypeInfo =
        (JsonTypeInfo<Result<JwksDocument>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Result<JwksDocument>));

    public async Task<Result<JwksDocument>> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync(
            options.Value.JwksEndpoint,
            ResultTypeInfo,
            cancellationToken);

        return response ?? Result.Fail("500", "JWKS endpoint returned an empty response.");
    }
}
