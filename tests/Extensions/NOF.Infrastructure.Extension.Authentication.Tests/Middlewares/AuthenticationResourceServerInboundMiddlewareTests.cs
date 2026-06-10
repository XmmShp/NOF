using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Application;
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
        var executionContext = new NOFContext();
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
        var jwksService = CreateJwksService([]);
        var executionContext = new NOFContext();
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
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        var jwksService = CreateJwksService([key]);
        var executionContext = new NOFContext();
        executionContext.SetHeader(NOFAbstractionConstants.Transport.Headers.Authorization, "Bearer not-a-jwt");
        var middleware = CreateMiddleware(userContext, jwksService, executionContext);

        var nextCalled = false;
        async Task Act()
        {
            await middleware.InvokeAsync(CreateInboundContext(), _ =>
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
        ResourceServerJwksCacheService jwksCacheService,
        NOFContext executionContext)
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
            NullLogger<AuthenticationResourceServerInboundMiddleware>.Instance,
            executionContext);
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
