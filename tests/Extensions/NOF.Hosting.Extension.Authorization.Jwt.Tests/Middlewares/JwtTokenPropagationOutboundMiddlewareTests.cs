using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Test;
using System.Security.Claims;
using Xunit;

namespace NOF.Hosting.Extension.Authorization.Jwt.Tests.Middlewares;

public sealed class JwtTokenPropagationOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithJwtPrincipal_ShouldWriteAuthorizationHeader()
    {
        var executionContext = new NOF.Contract.ExecutionContext();
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

        called.Should().BeTrue();
        executionContext[NOFContractConstants.Transport.Headers.Authorization].Should().Be("Bearer jwt-token");
    }

    [Fact]
    public async Task InvokeAsync_WithNonJwtPrincipal_ShouldNotWriteAuthorizationHeader()
    {
        var executionContext = new NOF.Contract.ExecutionContext();
        var userContext = new FakeUserContext();
        userContext.SetUser(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "custom")));

        var middleware = new JwtTokenPropagationOutboundMiddleware(
            userContext,
            Options.Create(new JwtTokenPropagationOptions()),
            executionContext);

        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, _ => ValueTask.CompletedTask, default);

        executionContext.ContainsKey(NOFContractConstants.Transport.Headers.Authorization).Should().BeFalse();
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

        options.HeaderName.Should().Be("X-Auth");
        options.TokenType.Should().Be("Token");
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
