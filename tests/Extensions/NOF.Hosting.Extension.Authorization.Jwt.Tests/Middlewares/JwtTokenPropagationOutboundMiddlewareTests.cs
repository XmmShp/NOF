using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Test;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Xunit;

namespace NOF.Hosting.Extension.Authorization.Jwt.Tests.Middlewares;

public sealed class JwtTokenPropagationOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithJwtPrincipal_ShouldWriteAuthorizationHeader()
    {
        var userContext = new FakeUserContext();
        var token = CreateUnsignedToken();
        userContext.User = JwtClaimsPrincipal.FromToken(token);

        var middleware = new JwtTokenPropagationOutboundMiddleware(
            userContext,
            Options.Create(new JwtTokenPropagationOptions()));

        var called = false;
        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, _ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(called);
        Assert.Equal("Bearer " + token,
        outboundContext.Headers[NOFAbstractionConstants.Transport.Headers.Authorization]);
    }

    private static string CreateUnsignedToken()
    {
        // Untrusted token used only to populate JwtClaimsPrincipal without validation.
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncode("""{"sub":"user-1"}""");
        return header + "." + payload + ".";
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Fact]
    public async Task InvokeAsync_WithNonJwtPrincipal_ShouldNotWriteAuthorizationHeader()
    {
        var userContext = new FakeUserContext();
        userContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "custom"));

        var middleware = new JwtTokenPropagationOutboundMiddleware(
            userContext,
            Options.Create(new JwtTokenPropagationOptions()));

        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, _ => ValueTask.CompletedTask, default);
        Assert.False(

        outboundContext.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.Authorization));
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

    private static RequestOutboundContext CreateOutboundContext()
    {
        return new RequestOutboundContext
        {
            Message = new object(),
            Services = new ServiceCollection().BuildServiceProvider(),
            ServiceType = typeof(object),
            MethodName = nameof(CreateOutboundContext)
        };
    }

    private sealed class FakeUserContext : IUserContext
    {
        private static readonly ClaimsPrincipal _anonymous = new();
        private ClaimsPrincipal _user = _anonymous;

        [AllowNull]
        public ClaimsPrincipal User
        {
            get => _user;
            set
            {
                StateChanging?.Invoke();
                _user = value ?? _anonymous;
                StateChanged?.Invoke();
            }
        }

        public event Action? StateChanging;
        public event Action? StateChanged;
    }
}
