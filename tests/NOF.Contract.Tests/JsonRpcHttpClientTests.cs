using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NOF.Contract.Tests;

public sealed class JsonRpcHttpClientTests
{
    [Fact]
    public async Task SendAsync_WritesJsonRpcEnvelopeAndReadsBusinessResult()
    {
        JsonDocument? capturedRequest = null;
        string? capturedHeader = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = JsonDocument.Parse(await request.Content!.ReadAsStreamAsync());
            capturedHeader = request.Headers.GetValues("x-test").Single();
            var id = capturedRequest.RootElement.GetProperty("id").GetString();
            var payload = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                result = Result.Success(new Pong("pong")),
                id
            }, JsonSerializerOptions.NOF);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };

        var result = await JsonRpcHttpClient.SendAsync<Ping, Result<Pong>>(
            httpClient,
            "/rpc",
            "Ping",
            new Ping("ping"),
            new Dictionary<string, string?> { ["x-test"] = "header" },
            JsonSerializerOptions.NOF.GetRequiredTypeInfo<Ping>(),
            JsonSerializerOptions.NOF.GetRequiredTypeInfo<Result<Pong>>(),
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("2.0", capturedRequest.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal("Ping", capturedRequest.RootElement.GetProperty("method").GetString());
        Assert.Equal("ping", capturedRequest.RootElement.GetProperty("params").GetProperty("value").GetString());
        Assert.Equal("header", capturedHeader);
        Assert.True(result.IsSuccess);
        Assert.Equal("pong", result.Value!.Value);
        capturedRequest.Dispose();
    }

    [Fact]
    public async Task SendAsync_ProjectsJsonRpcErrorToExpectedResult()
    {
        var handler = new StubHttpMessageHandler(async request =>
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStreamAsync());
            var id = document.RootElement.GetProperty("id").GetString();
            var payload = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                error = new { code = -32601, message = "Method not found" },
                id
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };

        var result = await JsonRpcHttpClient.SendAsync<Ping, Result<Pong>>(
            httpClient,
            "/rpc",
            "Missing",
            new Ping("ping"),
            [],
            JsonSerializerOptions.NOF.GetRequiredTypeInfo<Ping>(),
            JsonSerializerOptions.NOF.GetRequiredTypeInfo<Result<Pong>>(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("-32601", result.ErrorCode);
        Assert.Equal("Method not found", result.Message);
    }

    private sealed record Ping(string Value);

    private sealed record Pong(string Value);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
