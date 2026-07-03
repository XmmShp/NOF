using NOF.Abstraction;
using NOF.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NOF.Contract;
using System.Security.Claims;
using Xunit;
using MessageAccessTokenPropagationOutboundMiddleware = NOF.Infrastructure.JwtTokenPropagationOutboundMiddleware;

namespace NOF.Infrastructure.Tests.Authentication.Middlewares;

public sealed class AccessTokenPropagationOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithCommandOutboundContext_ShouldWriteAuthorizationHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(token));

        var middleware = new MessageAccessTokenPropagationOutboundMiddleware(userContext, NullLogger<MessageAccessTokenPropagationOutboundMiddleware>.Instance);
        var outboundContext = new CommandOutboundContext();

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer " + token, outboundContext.Headers[NOFAbstractionConstants.Transport.Headers.Authorization]);
    }

    [Fact]
    public async Task InvokeAsync_WithNotificationOutboundContext_ShouldWriteAuthorizationHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(token));

        var middleware = new MessageAccessTokenPropagationOutboundMiddleware(userContext, NullLogger<MessageAccessTokenPropagationOutboundMiddleware>.Instance);
        var outboundContext = new NotificationOutboundContext();

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer " + token, outboundContext.Headers[NOFAbstractionConstants.Transport.Headers.Authorization]);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyTokenType_ShouldWriteTokenWithoutLeadingSpace()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "jwt"),
            token,
            new JwtPropagation
            {
                HeaderName = "X-Auth",
                TokenType = string.Empty
            }));

        var middleware = new MessageAccessTokenPropagationOutboundMiddleware(userContext, NullLogger<MessageAccessTokenPropagationOutboundMiddleware>.Instance);
        var outboundContext = new CommandOutboundContext();

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal(token, outboundContext.Headers["X-Auth"]);
    }

    [Fact]
    public async Task InvokeAsync_WithCommandOutboundContextAndTokenExchangeEnabled_ShouldWriteExchangedToken()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "jwt"),
            token,
            new JwtPropagation
            {
                HeaderName = "X-Auth",
                TokenType = "Bearer",
                EnableTokenExchange = true
            }));

        var exchangeService = new StubJwtTokenExchangeService("exchanged-token");
        var middleware = new MessageAccessTokenPropagationOutboundMiddleware(
            userContext,
            NullLogger<MessageAccessTokenPropagationOutboundMiddleware>.Instance,
            exchangeService);
        var outboundContext = new CommandOutboundContext();

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer exchanged-token", outboundContext.Headers["X-Auth"]);
        Assert.Equal(token, exchangeService.LastSubjectToken);
    }

    [Fact]
    public async Task InvokeAsync_WithNotificationOutboundContextAndExplicitTokenExchangeHeader_ShouldWriteExchangedToken()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "jwt"),
            token,
            downstreamPropagation: null));

        var exchangeService = new StubJwtTokenExchangeService("explicit-exchanged-token");
        var middleware = new MessageAccessTokenPropagationOutboundMiddleware(
            userContext,
            NullLogger<MessageAccessTokenPropagationOutboundMiddleware>.Instance,
            exchangeService);
        var outboundContext = new NotificationOutboundContext(Context.Empty.WithTokenExchange("X-Service-Authorization"));

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer explicit-exchanged-token", outboundContext.Headers["X-Service-Authorization"]);
        Assert.Equal(token, exchangeService.LastSubjectToken);
    }

    [Fact]
    public async Task InvokeAsync_WithNotificationOutboundContextAndExplicitTokenExchangeHeaders_ShouldWriteExchangedTokenToEachHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "jwt"),
            token,
            downstreamPropagation: null));

        var exchangeService = new StubJwtTokenExchangeService("explicit-exchanged-token");
        var middleware = new MessageAccessTokenPropagationOutboundMiddleware(
            userContext,
            NullLogger<MessageAccessTokenPropagationOutboundMiddleware>.Instance,
            exchangeService);
        var outboundContext = new NotificationOutboundContext(Context.Empty.WithTokenExchange("X-Service-Authorization", "X-Secondary-Authorization"));

        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Bearer explicit-exchanged-token", outboundContext.Headers["X-Service-Authorization"]);
        Assert.Equal("Bearer explicit-exchanged-token", outboundContext.Headers["X-Secondary-Authorization"]);
        Assert.Equal(token, exchangeService.LastSubjectToken);
    }

    [Fact]
    public void AuthenticationTokenSourceOptions_DefaultDownstreamPropagation_ShouldDisableTokenExchange()
    {
        var options = new AuthenticationTokenSourceOptions();

        Assert.NotNull(options.DownstreamPropagation);
        Assert.False(options.DownstreamPropagation!.EnableTokenExchange);
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
            _ = propagation;
            _ = cancellationToken;
            LastSubjectToken = subjectToken;
            return ValueTask.FromResult(exchangedToken);
        }
    }
}
