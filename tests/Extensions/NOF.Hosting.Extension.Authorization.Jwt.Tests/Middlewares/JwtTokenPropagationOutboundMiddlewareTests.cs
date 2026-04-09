using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Test;
using System.Security.Claims;
using Xunit;

namespace NOF.Hosting.Extension.Authorization.Jwt.Tests.Middlewares;

public sealed class JwtTokenPropagationOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithJwtPrincipal_ShouldWriteAuthorizationHeader()
    {
        var executionContext = new ExecutionContext();
        var userContext = new FakeUserContext();
        userContext.SetUser(new JwtClaimsPrincipal(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "jwt")),
            token: "jwt-token"));

        var middleware = new JwtTokenPropagationOutboundMiddleware(
            userContext,
            Options.Create(new JwtTokenPropagationOptions()),
            executionContext);

        var called = false;
        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, _ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(

        called);
        Assert.Equal("Bearer jwt-token",
        executionContext[NOFHostingConstants.Transport.Headers.Authorization]);
    }

    [Fact]
    public async Task InvokeAsync_WithNonJwtPrincipal_ShouldNotWriteAuthorizationHeader()
    {
        var executionContext = new ExecutionContext();
        var userContext = new FakeUserContext();
        userContext.SetUser(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "custom")));

        var middleware = new JwtTokenPropagationOutboundMiddleware(
            userContext,
            Options.Create(new JwtTokenPropagationOptions()),
            executionContext);

        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, _ => ValueTask.CompletedTask, default);
        Assert.False(

        executionContext.ContainsKey(NOFHostingConstants.Transport.Headers.Authorization));
    }

    [Fact]
    public async Task AddJwtTokenPropagation_ShouldApplyConfiguredOptions()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtTokenPropagation(options =>
        {
            options.HeaderName = "X-Auth";
            options.TokenType = "Token";
        });

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var options = scope.GetRequiredService<IOptions<JwtTokenPropagationOptions>>().Value;
        Assert.Equal("X-Auth",

        options.HeaderName);
        Assert.Equal("Token",
        options.TokenType);
    }

    private static OutboundContext CreateOutboundContext()
    {
        return new OutboundContext
        {
            Message = new object(),
            Services = new ServiceCollection().BuildServiceProvider()
        };
    }

    private sealed class FakeUserContext : IUserContext
    {
        public ClaimsPrincipal User { get; private set; } = new();
        public event Action? StateChanging;
        public event Action? StateChanged;

        public void SetUser(ClaimsPrincipal user)
        {
            StateChanging?.Invoke();
            User = user;
            StateChanged?.Invoke();
        }

        public void UnsetUser()
        {
            StateChanging?.Invoke();
            User = new ClaimsPrincipal();
            StateChanged?.Invoke();
        }
    }
}

