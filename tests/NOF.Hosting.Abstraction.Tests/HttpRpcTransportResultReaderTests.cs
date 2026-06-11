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

        var result = await HttpRpcTransportResultReader.ReadAsync(
            response,
            CreateTypeInfo<TokenResponse>(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("abc", result.Value!.Token);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("trace-1", result.Headers["X-Trace-Id"]);
    }

    [Fact]
    public async Task ReadFailureAsync_WhenResponseIsOAuthError_PreservesStatusHeadersAndBody()
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

        var result = await HttpRpcTransportResultReader.ReadFailureAsync<TokenResponse>(response, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("invalid_token", result.Body?.ToString());
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(
            "Bearer error=\"invalid_token\", error_description=\"access token expired.\"",
            result.Headers["WWW-Authenticate"]);
    }

    public sealed record TokenResponse(string Token);

    private static JsonTypeInfo<T> CreateTypeInfo<T>()
        => (JsonTypeInfo<T>)new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        }.GetTypeInfo(typeof(T));
}
