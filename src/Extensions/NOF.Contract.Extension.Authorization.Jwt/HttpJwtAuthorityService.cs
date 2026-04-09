using NOF.Hosting;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Contract.Extension.Authorization.Jwt;

public sealed partial class HttpJwtAuthorityService(
    HttpClient httpClient,
    IOutboundPipelineExecutor outboundPipeline,
    IExecutionContext executionContext,
    IServiceProvider serviceProvider) : IJwtAuthorityService
{
    private static readonly JsonTypeInfo<GenerateJwtTokenRequest> GenerateJwtTokenRequestTypeInfo =
        (JsonTypeInfo<GenerateJwtTokenRequest>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(GenerateJwtTokenRequest));
    private static readonly JsonTypeInfo<Result<GenerateJwtTokenResponse>> GenerateJwtTokenResponseTypeInfo =
        (JsonTypeInfo<Result<GenerateJwtTokenResponse>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Result<GenerateJwtTokenResponse>));
    private static readonly JsonTypeInfo<ValidateJwtRefreshTokenRequest> ValidateJwtRefreshTokenRequestTypeInfo =
        (JsonTypeInfo<ValidateJwtRefreshTokenRequest>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(ValidateJwtRefreshTokenRequest));
    private static readonly JsonTypeInfo<Result<ValidateJwtRefreshTokenResponse>> ValidateJwtRefreshTokenResponseTypeInfo =
        (JsonTypeInfo<Result<ValidateJwtRefreshTokenResponse>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Result<ValidateJwtRefreshTokenResponse>));
    private static readonly JsonTypeInfo<RevokeJwtRefreshTokenRequest> RevokeJwtRefreshTokenRequestTypeInfo =
        (JsonTypeInfo<RevokeJwtRefreshTokenRequest>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(RevokeJwtRefreshTokenRequest));
    private static readonly JsonTypeInfo<Result> ResultTypeInfo =
        (JsonTypeInfo<Result>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Result));

    public Task<Result<GenerateJwtTokenResponse>> GenerateJwtTokenAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default)
        => PostAsync(JwtAuthorizationEndpoints.Token, request, GenerateJwtTokenRequestTypeInfo, GenerateJwtTokenResponseTypeInfo, cancellationToken);

    public Task<Result<ValidateJwtRefreshTokenResponse>> ValidateJwtRefreshTokenAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
        => PostAsync(JwtAuthorizationEndpoints.Introspect, request, ValidateJwtRefreshTokenRequestTypeInfo, ValidateJwtRefreshTokenResponseTypeInfo, cancellationToken);

    public Task<Result> RevokeJwtRefreshTokenAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
        => PostAsync(JwtAuthorizationEndpoints.Revocation, request, RevokeJwtRefreshTokenRequestTypeInfo, ResultTypeInfo, cancellationToken);

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken)
        where TRequest : notnull
        where TResponse : class, IResult
    {
        var context = new OutboundContext
        {
            Message = request,
            Services = serviceProvider
        };

        TResponse? result = null;

        await outboundPipeline.ExecuteAsync(context, async ct =>
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(request, requestTypeInfo)
            };

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

            result = await response.Content.ReadFromJsonAsync(responseTypeInfo, ct).ConfigureAwait(false);
            context.Response = result;
        }, cancellationToken).ConfigureAwait(false);

        return result!;
    }
}
