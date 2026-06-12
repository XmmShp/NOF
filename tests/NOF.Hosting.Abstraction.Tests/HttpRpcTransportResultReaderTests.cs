using NOF.Contract;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace NOF.Hosting.Abstraction.Tests;

public sealed class HttpRpcTransportResultReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenResponseSucceeds_ReturnsBusinessResult()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"isSuccess":true,"errorCode":"","message":"","value":{"token":"abc"},"extra":{}}""",
                Encoding.UTF8,
                "application/json")
        };

        var result = await HttpRpcTransportResultReader.ReadAsync(
            response,
            CreateTypeInfo<Result<TokenResponse>>(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("abc", result.Value!.Token);
    }

    [Fact]
    public async Task ReadFailureAsync_WhenResponseIsOAuthError_ReturnsTransportFailure()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                """{"error":"invalid_token","error_description":"access token expired."}""",
                Encoding.UTF8,
                "application/json")
        };

        var result = await HttpRpcTransportResultReader.ReadFailureAsync<Result<TokenResponse>>(response, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("401", result.ErrorCode);
        Assert.Contains("invalid_token", result.Message);
    }

    [Fact]
    public async Task ReadAsync_WhenResponseIsRedirect_ReturnsTransportFailure()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://example.com/callback");

        var result = await HttpRpcTransportResultReader.ReadAsync(
            response,
            CreateTypeInfo<Result>(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("302", result.ErrorCode);
    }

    [Fact]
    public async Task ReadAsync_WhenResponseIsSuccess_ReadsContractFailureBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"isSuccess":false,"errorCode":"invalid_token","message":"access token expired.","value":null,"extra":{}}""",
                Encoding.UTF8,
                "application/json")
        };

        var result = await HttpRpcTransportResultReader.ReadAsync(
            response,
            CreateTypeInfo<Result<TokenResponse>>(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal("invalid_token", result.ErrorCode);
    }

    public sealed record TokenResponse(string Token);

    private static JsonTypeInfo<T> CreateTypeInfo<T>()
        => (JsonTypeInfo<T>)new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        }.GetTypeInfo(typeof(T));
}
