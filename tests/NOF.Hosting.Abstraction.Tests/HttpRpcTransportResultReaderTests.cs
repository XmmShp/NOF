using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace NOF.Hosting.Abstraction.Tests;

public sealed class HttpRpcTransportResultReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenResponseSucceeds_PreservesResponseMetadatas()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"token":"abc"}""", Encoding.UTF8, "application/json")
        };
        response.Headers.TryAddWithoutValidation("X-Trace-Id", "trace-1");

        var transportResult = await HttpRpcTransportResultReader.ReadAsync(
            response,
            CreateTypeInfo<Result<TokenResponse>>(),
            CancellationToken.None);

        var result = transportResult.Response;
        Assert.True(result.IsSuccess);
        Assert.Equal("abc", result.Value!.Token);
        Assert.True(HttpTransportMetadata.TryGetStatusCode(transportResult.Metadatas, out var statusCode));
        Assert.Equal(200, statusCode);
        Assert.Equal("trace-1", HttpTransportMetadata.GetHeaders(transportResult.Metadatas)["X-Trace-Id"]);
    }

    [Fact]
    public async Task ReadFailureAsync_WhenResponseIsOAuthError_PreservesStatusHeadersWithoutBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                """{"error":"invalid_token","error_description":"access token expired."}""",
                Encoding.UTF8,
                "application/json")
        };
        response.Headers.TryAddWithoutValidation(
            "WWW-Authenticate",
            "Bearer error=\"invalid_token\", error_description=\"access token expired.\"");

        var transportResult = await HttpRpcTransportResultReader.ReadFailureAsync<Result<TokenResponse>>(response, CancellationToken.None);

        var result = transportResult.Response;
        Assert.False(result.IsSuccess);
        Assert.True(HttpTransportMetadata.TryGetStatusCode(transportResult.Metadatas, out var statusCode));
        Assert.Equal(401, statusCode);
        Assert.Equal(
            "Bearer error=\"invalid_token\", error_description=\"access token expired.\"",
            HttpTransportMetadata.GetHeaders(transportResult.Metadatas)["WWW-Authenticate"]);
    }

    [Fact]
    public async Task ReadAsync_WhenTransportSuccessHeaderIsTrue_DoesNotTreatRedirectAsTransportFailure()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://example.com/callback");
        response.Headers.TryAddWithoutValidation(NOFAbstractionConstants.Transport.Headers.RpcSuccess, bool.TrueString);

        var transportResult = await HttpRpcTransportResultReader.ReadAsync(
            response,
            CreateTypeInfo<Result>(),
            CancellationToken.None);

        var result = transportResult.Response;
        Assert.True(result.IsSuccess);
        Assert.True(HttpTransportMetadata.TryGetStatusCode(transportResult.Metadatas, out var statusCode));
        Assert.Equal(302, statusCode);
        Assert.Equal("https://example.com/callback", HttpTransportMetadata.GetHeaders(transportResult.Metadatas)["Location"]);
    }

    [Fact]
    public async Task ReadAsync_WhenTransportSucceedsWithNonContractFailureBody_PreservesBodyAsJson()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                """{"error":"invalid_token","error_description":"access token expired."}""",
                Encoding.UTF8,
                "application/json")
        };
        response.Headers.TryAddWithoutValidation(NOFAbstractionConstants.Transport.Headers.RpcSuccess, bool.TrueString);

        var transportResult = await HttpRpcTransportResultReader.ReadAsync(
            response,
            CreateTypeInfo<Result<TokenResponse>>(),
            CancellationToken.None);

        var result = transportResult.Response;
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.True(HttpTransportMetadata.TryGetStatusCode(transportResult.Metadatas, out var statusCode));
        Assert.Equal(401, statusCode);
    }

    public sealed record TokenResponse(string Token);

    private static JsonTypeInfo<T> CreateTypeInfo<T>()
        => (JsonTypeInfo<T>)new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        }.GetTypeInfo(typeof(T));
}
