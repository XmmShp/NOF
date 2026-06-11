using NOF.Abstraction;
using NOF.Test;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace NOF.Hosting.Extension.Authentication.Tests.Middlewares;

public sealed class AccessTokenPropagationOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithJwtPrincipal_ShouldWriteAuthorizationHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new AccessTokenIdentity(token));

        var middleware = new AccessTokenPropagationOutboundMiddleware(userContext);

        var called = false;
        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) =>
        {
            called = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(called);
        Assert.Equal("Bearer " + token, outboundContext.Headers[NOFAbstractionConstants.Transport.Headers.Authorization]);
    }

    private static string CreateUnsignedToken()
    {
        // Untrusted token used only to populate AccessTokenIdentity without validation.
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
        var userContext = new UserContext();
        userContext.User.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "custom"));

        var middleware = new AccessTokenPropagationOutboundMiddleware(userContext);

        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);
        Assert.False(outboundContext.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.Authorization));
    }

    [Fact]
    public async Task InvokeAsync_WithCustomDownstreamPropagation_ShouldWriteConfiguredHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new AccessTokenIdentity(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "jwt"),
            token,
            new AccessTokenPropagation
            {
                HeaderName = "X-Auth",
                TokenType = "Token"
            }));

        var middleware = new AccessTokenPropagationOutboundMiddleware(userContext);
        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Token " + token, outboundContext.Headers["X-Auth"]);
    }

    private static RequestOutboundContext CreateOutboundContext()
    {
        return new RequestOutboundContext
        {
            ServiceType = typeof(object),
            MethodInfo = typeof(AccessTokenPropagationOutboundMiddlewareTests)
                .GetMethod(nameof(CreateOutboundContext), BindingFlags.NonPublic | BindingFlags.Static)!
        };
    }

    [Fact]
    public void AddAccessTokenPropagation_ShouldRegisterOutboundMiddleware()
    {
        var builder = NOFTestAppBuilder.Create();

        builder.AddAccessTokenPropagation();

        Assert.Contains(builder.Services, descriptor => descriptor.ImplementationType == typeof(AccessTokenPropagationOutboundMiddleware));
    }
}
