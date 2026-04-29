using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Application;
using System.Security.Cryptography;
using Xunit;
using TransparentInfos = NOF.Application.TransparentInfos;

namespace NOF.Infrastructure.Extension.Authorization.Jwt.Tests.Middlewares;

public sealed class JwtResourceServerInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenAuthorizationHeaderMissing_ShouldContinueWithoutValidation()
    {
        var userContext = new UserContext();
        var jwksService = CreateCachedJwksService([]);
        var executionContext = new TransparentInfos();
        var middleware = CreateMiddleware(userContext, jwksService, executionContext);

        var nextCalled = false;
        await middleware.InvokeAsync(CreateInboundContext(), _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(nextCalled);
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_WhenKeysUnavailable_ShouldContinueAndKeepHeader()
    {
        var userContext = new UserContext();
        var jwksService = CreateCachedJwksService([]);
        var executionContext = new TransparentInfos();
        executionContext.SetHeader(NOFAbstractionConstants.Transport.Headers.Authorization, "Bearer invalid-token");
        var middleware = CreateMiddleware(userContext, jwksService, executionContext);

        var nextCalled = false;
        await middleware.InvokeAsync(CreateInboundContext(), _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(nextCalled);
        Assert.True(executionContext.ContainsHeader(NOFAbstractionConstants.Transport.Headers.Authorization));
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_WhenTokenInvalid_ShouldContinueWithoutThrowing()
    {
        var userContext = new UserContext();
        using var rsa = RSA.Create(2048);
        var key = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow
        };
        var jwksService = CreateCachedJwksService([key]);
        var executionContext = new TransparentInfos();
        executionContext.SetHeader(NOFAbstractionConstants.Transport.Headers.Authorization, "Bearer not-a-jwt");
        var middleware = CreateMiddleware(userContext, jwksService, executionContext);

        var nextCalled = false;
        var act = async () => await middleware.InvokeAsync(CreateInboundContext(), _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.Null(await Record.ExceptionAsync(act));
        Assert.True(nextCalled);
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);
    }

    private static JwtResourceServerInboundMiddleware CreateMiddleware(
        IUserContext userContext,
        CachedJwksService jwksService,
        ITransparentInfos executionContext)
    {
        return new JwtResourceServerInboundMiddleware(
            userContext,
            jwksService,
            Microsoft.Extensions.Options.Options.Create(new JwtResourceServerOptions
            {
                HeaderName = NOFAbstractionConstants.Transport.Headers.Authorization,
                TokenType = "Bearer",
                JwksEndpoint = "https://auth.local/.well-known/jwks.json",
                RequireHttpsMetadata = true
            }),
            NullLogger<JwtResourceServerInboundMiddleware>.Instance,
            executionContext);
    }

    private static CachedJwksService CreateCachedJwksService(IReadOnlyList<ManagedSigningKey> keys)
    {
        var signingKeyService = new FakeSigningKeyService(keys);
        var rootProvider = new FakeServiceProvider(typeof(ISigningKeyService), signingKeyService);
        return new CachedJwksService(new FakeServiceScopeFactory(rootProvider), signingKeyService);
    }

    private static RequestInboundContext CreateInboundContext()
    {
        return new RequestInboundContext
        {
            Message = new object(),
            HandlerType = typeof(object),
            ServiceType = typeof(object),
            MethodName = nameof(ToString)
        };
    }

    private sealed class FakeSigningKeyService(IReadOnlyList<ManagedSigningKey> keys) : ISigningKeyService
    {
        public ManagedSigningKey CurrentSigningKey => keys.FirstOrDefault() ?? throw new InvalidOperationException("No signing keys configured.");

        public IReadOnlyList<ManagedSigningKey> AllKeys => keys;

        public void RotateKey()
        {
        }
    }

    private sealed class FakeServiceProvider(Type serviceType, object? service) : IServiceProvider
    {
        public object? GetService(Type requestedType) => requestedType == serviceType ? service : null;
    }

    private sealed class FakeServiceScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope(serviceProvider);
    }

    private sealed class FakeServiceScope(IServiceProvider serviceProvider) : IServiceScope
    {
        public IServiceProvider ServiceProvider => serviceProvider;

        public void Dispose()
        {
        }
    }
}
