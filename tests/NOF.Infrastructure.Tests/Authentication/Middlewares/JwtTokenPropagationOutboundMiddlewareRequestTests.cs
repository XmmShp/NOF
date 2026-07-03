using Microsoft.Extensions.Logging.Abstractions;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.Middlewares;

public sealed class JwtTokenPropagationOutboundMiddlewareRequestTests
{
    [Fact]
    public async Task InvokeAsync_WithTokenExchangeEnabled_ShouldWriteExchangedToken()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new System.Security.Claims.ClaimsIdentity(authenticationType: "jwt"),
            token,
            new JwtPropagation
            {
                HeaderName = "X-Auth",
                TokenType = "Bearer",
                EnableTokenExchange = true
            }));
        var tokenExchangeService = new StubJwtTokenExchangeService("exchanged-token");
        var middleware = new NOF.Hosting.JwtTokenPropagationOutboundMiddleware(userContext, NullLogger<NOF.Hosting.JwtTokenPropagationOutboundMiddleware>.Instance, tokenExchangeService);
        var outboundContext = CreateOutboundContext();

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer exchanged-token", outboundContext.Headers["X-Auth"]);
        Assert.Equal(token, tokenExchangeService.LastSubjectToken);
    }

    [Fact]
    public async Task InvokeAsync_WithTokenExchangeDisabled_ShouldWriteOriginalToken()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new System.Security.Claims.ClaimsIdentity(authenticationType: "jwt"),
            token,
            new JwtPropagation
            {
                HeaderName = "X-Auth",
                TokenType = "Bearer",
                EnableTokenExchange = false
            }));
        var tokenExchangeService = new StubJwtTokenExchangeService("exchanged-token");
        var middleware = new NOF.Hosting.JwtTokenPropagationOutboundMiddleware(userContext, NullLogger<NOF.Hosting.JwtTokenPropagationOutboundMiddleware>.Instance, tokenExchangeService);
        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer " + token, outboundContext.Headers["X-Auth"]);
        Assert.Null(tokenExchangeService.LastSubjectToken);
    }

    [Fact]
    public async Task InvokeAsync_WithTokenExchangeEnabled_ShouldDelegateToExchangeService()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new System.Security.Claims.ClaimsIdentity(authenticationType: "jwt"),
            CreateUnsignedToken(),
            new JwtPropagation
            {
                EnableTokenExchange = true
            }));
        var middleware = new NOF.Hosting.JwtTokenPropagationOutboundMiddleware(userContext, NullLogger<NOF.Hosting.JwtTokenPropagationOutboundMiddleware>.Instance, new StubJwtTokenExchangeService("exchanged-token"));
        var outboundContext = CreateOutboundContext();

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer exchanged-token", outboundContext.Headers[NOFAbstractionConstants.Transport.Headers.Authorization]);
    }

    [Fact]
    public async Task InvokeAsync_WithExplicitTokenExchangeHeader_ShouldWriteExchangedToken()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new System.Security.Claims.ClaimsIdentity(authenticationType: "jwt"),
            token,
            downstreamPropagation: null));
        var tokenExchangeService = new StubJwtTokenExchangeService("explicit-exchanged-token");
        var middleware = new NOF.Hosting.JwtTokenPropagationOutboundMiddleware(userContext, NullLogger<NOF.Hosting.JwtTokenPropagationOutboundMiddleware>.Instance, tokenExchangeService);
        var outboundContext = new RequestOutboundContext(Context.Empty.WithTokenExchange("X-Service-Authorization"))
        {
            ServiceType = typeof(object),
            MethodInfo = typeof(JwtTokenPropagationOutboundMiddlewareRequestTests)
                .GetMethod(nameof(CreateOutboundContext), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
        };

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer explicit-exchanged-token", outboundContext.Headers["X-Service-Authorization"]);
        Assert.Equal(token, tokenExchangeService.LastSubjectToken);
    }

    [Fact]
    public async Task InvokeAsync_WithExplicitTokenExchangeHeaders_ShouldWriteExchangedTokenToEachHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new System.Security.Claims.ClaimsIdentity(authenticationType: "jwt"),
            token,
            downstreamPropagation: null));
        var tokenExchangeService = new StubJwtTokenExchangeService("explicit-exchanged-token");
        var middleware = new NOF.Hosting.JwtTokenPropagationOutboundMiddleware(userContext, NullLogger<NOF.Hosting.JwtTokenPropagationOutboundMiddleware>.Instance, tokenExchangeService);
        var outboundContext = new RequestOutboundContext(Context.Empty.WithTokenExchange("X-Service-Authorization", "X-Secondary-Authorization"))
        {
            ServiceType = typeof(object),
            MethodInfo = typeof(JwtTokenPropagationOutboundMiddlewareRequestTests)
                .GetMethod(nameof(CreateOutboundContext), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
        };

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer explicit-exchanged-token", outboundContext.Headers["X-Service-Authorization"]);
        Assert.Equal("Bearer explicit-exchanged-token", outboundContext.Headers["X-Secondary-Authorization"]);
        Assert.Equal(token, tokenExchangeService.LastSubjectToken);
    }

    private static RequestOutboundContext CreateOutboundContext()
    {
        return new RequestOutboundContext
        {
            ServiceType = typeof(object),
            MethodInfo = typeof(JwtTokenPropagationOutboundMiddlewareRequestTests)
                .GetMethod(nameof(CreateOutboundContext), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
        };
    }

    private static string CreateUnsignedToken()
    {
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncode("""{"sub":"user-1"}""");
        return header + "." + payload + ".";
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class StubJwtTokenExchangeService(string exchangedToken) : IJwtTokenExchangeService
    {
        public string? LastSubjectToken { get; private set; }
        public ValueTask<string> ExchangeTokenAsync(string subjectToken, JwtPropagation propagation, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            LastSubjectToken = subjectToken;
            _ = propagation;
            return ValueTask.FromResult(exchangedToken);
        }
    }
}
