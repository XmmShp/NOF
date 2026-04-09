using NOF.Abstraction;
using NOF.Hosting;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Contract.Extension.Authorization.Jwt;

public sealed partial class HttpJwksService(
    HttpClient httpClient,
    IOutboundPipelineExecutor outboundPipeline,
    IExecutionContext executionContext,
    IServiceProvider serviceProvider) : IJwksService
{
    private static readonly JsonTypeInfo<Result<JwksDocument>> ResultTypeInfo =
        (JsonTypeInfo<Result<JwksDocument>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Result<JwksDocument>));

    public async Task<Result<JwksDocument>> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        var context = new OutboundContext
        {
            Message = new object(),
            Services = serviceProvider
        };

        Result<JwksDocument>? result = null;

        await outboundPipeline.ExecuteAsync(context, async ct =>
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, JwtAuthorizationEndpoints.Jwks);

            foreach (var (key, value) in executionContext)
            {
                if (value is null)
                {
                    continue;
                }

                httpRequest.Headers.Remove(key);
                httpRequest.Headers.TryAddWithoutValidation(key, value);
            }

            using var response = await httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            result = await response.Content.ReadFromJsonAsync(ResultTypeInfo, ct).ConfigureAwait(false);
            context.Response = result;
        }, cancellationToken).ConfigureAwait(false);

        return result!;
    }
}
