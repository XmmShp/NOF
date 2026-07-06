using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Reflection;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.Middlewares;

public sealed class ServiceTokenOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithRequestOutboundContext_ShouldWriteConfiguredHeader()
    {
        var tokenService = new StubClientCredentialsTokenService();
        var middleware = CreateMiddleware(tokenService);
        var outboundContext = new RequestOutboundContext(Context.Empty.WithServiceToken("Authorization"))
        {
            ServiceType = typeof(object),
            MethodInfo = typeof(ServiceTokenOutboundMiddlewareTests)
                .GetMethod(nameof(CreateMiddleware), BindingFlags.NonPublic | BindingFlags.Static)!
        };

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer service-token", outboundContext.Headers["Authorization"]);
        Assert.Equal("client-id", tokenService.LastRequest?.ClientId);
        Assert.Equal("client-secret", tokenService.LastRequest?.ClientSecret);
    }

    [Fact]
    public async Task InvokeAsync_WithCommandOutboundContext_ShouldOverwriteExistingHeader()
    {
        var middleware = CreateMiddleware(new StubClientCredentialsTokenService());
        var outboundContext = new CommandOutboundContext(Context.Empty.WithServiceToken("Authorization"));
        outboundContext.Headers["Authorization"] = "Bearer user-token";

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer service-token", outboundContext.Headers["Authorization"]);
    }

    [Fact]
    public async Task InvokeAsync_WithNotificationOutboundContext_ShouldWriteCustomHeader()
    {
        var middleware = CreateMiddleware(new StubClientCredentialsTokenService());
        var outboundContext = new NotificationOutboundContext(Context.Empty.WithServiceToken("X-Service-Authorization"));

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer service-token", outboundContext.Headers["X-Service-Authorization"]);
    }

    [Fact]
    public async Task InvokeAsync_WithoutServiceTokenMarker_ShouldSkipTokenRequest()
    {
        var tokenService = new StubClientCredentialsTokenService();
        var middleware = CreateMiddleware(tokenService);
        var outboundContext = new CommandOutboundContext(Context.Empty);

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Null(tokenService.LastRequest);
        Assert.Empty(outboundContext.Headers);
    }

    private static ServiceTokenOutboundMiddleware CreateMiddleware(StubClientCredentialsTokenService tokenService)
        => new(
            tokenService,
            Options.Create(new AuthenticationResourceServerOptions
            {
                TokenExchangeClient = new AuthenticationClientCredentialsOptions
                {
                    ClientId = "client-id",
                    ClientSecret = "client-secret"
                }
            }),
            NullLogger<ServiceTokenOutboundMiddleware>.Instance);

    private sealed class StubClientCredentialsTokenService : IClientCredentialsTokenService
    {
        public ClientCredentialsTokenRequest? LastRequest { get; private set; }

        public ValueTask<ClientCredentialsTokenResponse> GetTokenAsync(
            ClientCredentialsTokenRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastRequest = request;
            return ValueTask.FromResult(new ClientCredentialsTokenResponse
            {
                AccessToken = "service-token",
                TokenType = "Bearer"
            });
        }
    }
}
