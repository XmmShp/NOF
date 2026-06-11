using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using System.Security.Cryptography;
using Xunit;

namespace NOF.Infrastructure.Extension.Authentication.Tests.Middlewares;

public sealed class AuthenticationResourceServerInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenAuthorizationHeaderMissing_ShouldContinueWithoutValidation()
    {
        var userContext = new UserContext();
        var jwksService = CreateJwksService([]);
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundContext = CreateInboundContext();

        var nextCalled = false;
        await middleware.InvokeAsync(inboundContext, _ =>
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
        var jwksService = CreateJwksService([]);
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundContext = CreateInboundContext(
            Context.Empty.WithHeader(NOFAbstractionConstants.Transport.Headers.Authorization, "Bearer invalid-token"));

        var nextCalled = false;
        await middleware.InvokeAsync(inboundContext, _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(nextCalled);
        Assert.True(inboundContext.Context.TryGetHeader(NOFAbstractionConstants.Transport.Headers.Authorization, out _));
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
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        var jwksService = CreateJwksService([key]);
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundContext = CreateInboundContext(
            Context.Empty.WithHeader(NOFAbstractionConstants.Transport.Headers.Authorization, "Bearer not-a-jwt"));

        var nextCalled = false;
        async Task Act()
        {
            await middleware.InvokeAsync(inboundContext, _ =>
            {
                nextCalled = true;
                return ValueTask.CompletedTask;
            }, default);
        }

        Assert.Null(await Record.ExceptionAsync(Act));
        Assert.True(nextCalled);
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);
    }

    private static AuthenticationResourceServerInboundMiddleware CreateMiddleware(
        IUserContext userContext,
        ResourceServerJwksCacheService jwksCacheService)
    {
        return new AuthenticationResourceServerInboundMiddleware(
            userContext,
            jwksCacheService,
            Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
            {
                JwksEndpoint = "https://auth.local/.well-known/jwks.json",
                RequireHttpsMetadata = true,
                Sources =
                [
                    new AuthenticationTokenSourceOptions
                    {
                        HeaderName = NOFAbstractionConstants.Transport.Headers.Authorization,
                        TokenType = "Bearer"
                    }
                ]
            }),
            NullLogger<AuthenticationResourceServerInboundMiddleware>.Instance);
    }

    private static ResourceServerJwksCacheService CreateJwksService(IReadOnlyList<ManagedSigningKey> keys)
    {
        var signingKeyService = new FakeSigningKeyService([.. keys]);
        var services = new ServiceCollection();
        services.AddScoped<IJwksService>(_ => new LocalJwksService(signingKeyService));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new ResourceServerJwksCacheService(
            scopeFactory,
            Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions()),
            TimeProvider.System);
    }

    private static RequestInboundContext CreateInboundContext(Context? context = null)
    {
        return new RequestInboundContext
        {
            Context = context ?? Context.Empty,
            Message = new object(),
            HandlerType = typeof(object),
            ResponseType = typeof(Result),
            ServiceType = typeof(object),
            MethodName = nameof(ToString)
        };
    }

    private sealed class FakeSigningKeyService(ManagedSigningKey[] keys) : ISigningKeyService
    {
        public Task<ManagedSigningKey> GetCurrentSigningKeyAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(keys.FirstOrDefault() ?? throw new InvalidOperationException("No signing keys configured."));
        }

        public Task<ManagedSigningKey[]> GetAllKeysAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(keys);
        }

        public Task RotateKeyAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

}
