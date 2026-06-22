using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace NOF.Hosting.Abstraction.Tests;

public sealed class JwtTokenPropagationOutboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithJwtPrincipal_ShouldWriteAuthorizationHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(token));

        var middleware = new JwtTokenPropagationOutboundMiddleware(userContext);

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

        var middleware = new JwtTokenPropagationOutboundMiddleware(userContext);

        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);
        Assert.False(outboundContext.Headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.Authorization));
    }

    [Fact]
    public async Task InvokeAsync_WithCustomDownstreamPropagation_ShouldWriteConfiguredHeader()
    {
        var userContext = new UserContext();
        var token = CreateUnsignedToken();
        userContext.User.AddIdentity(new JwtClaimsIdentity(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "jwt"),
            token,
            new JwtPropagation
            {
                HeaderName = "X-Auth",
                TokenType = "Token"
            }));

        var middleware = new JwtTokenPropagationOutboundMiddleware(userContext);
        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Token " + token, outboundContext.Headers["X-Auth"]);
    }

    [Fact]
    public void JwtPropagation_ShouldDisableTokenExchangeByDefault()
    {
        var propagation = new JwtPropagation();

        Assert.False(propagation.EnableTokenExchange);
    }

    [Fact]
    public void ClaimsPrincipal_ProxyServiceName_ShouldReturnProxyClaimValue()
    {
        var userContext = new UserContext();
        userContext.User.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.ProxyServiceName, "proxy-service")], "jwt"));

        Assert.Equal("proxy-service", userContext.User.ProxyServiceName);
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

        var middleware = new JwtTokenPropagationOutboundMiddleware(userContext);
        var outboundContext = CreateOutboundContext();
        await middleware.InvokeAsync(outboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal(token, outboundContext.Headers["X-Auth"]);
    }


    private static RequestOutboundContext CreateOutboundContext()
    {
        return new RequestOutboundContext
        {
            ServiceType = typeof(object),
            MethodInfo = typeof(JwtTokenPropagationOutboundMiddlewareTests)
                .GetMethod(nameof(CreateOutboundContext), BindingFlags.NonPublic | BindingFlags.Static)!
        };
    }

    [Fact]
    public void AddAccessTokenPropagation_ShouldRegisterOutboundMiddleware()
    {
        var builder = new TestAppBuilder();

        builder.AddJwtPropagation();

        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IRequestOutboundMiddleware)
            && descriptor.ImplementationType == typeof(JwtTokenPropagationOutboundMiddleware));
    }

    private sealed class TestAppBuilder : NOFAppBuilder<FakeHost>
    {
        private readonly ConfigurationManager _configuration = new();
        private readonly Dictionary<object, object> _properties = [];
        private readonly FakeHostEnvironment _environment = new();

        public override IDictionary<object, object> Properties => _properties;
        public override IConfigurationManager Configuration => _configuration;
        public override IHostEnvironment Environment => _environment;
        public override ILoggingBuilder Logging => throw new NotSupportedException();
        public override IMetricsBuilder Metrics => throw new NotSupportedException();
        public override IServiceCollection Services { get; } = new ServiceCollection();

        public override void ConfigureContainer<TContainerBuilder>(
            IServiceProviderFactory<TContainerBuilder> factory,
            Action<TContainerBuilder>? configure = null)
        {
            _ = factory;
            _ = configure;
        }

        protected override Task<FakeHost> BuildApplicationAsync()
            => Task.FromResult(new FakeHost());
    }

    private sealed class FakeHost : IHost
    {
        public IServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();

        public void Dispose()
        {
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "NOF.Hosting.Abstraction.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
