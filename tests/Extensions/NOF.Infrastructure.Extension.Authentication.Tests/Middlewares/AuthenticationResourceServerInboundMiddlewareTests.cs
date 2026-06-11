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
        var inboundMetadata = CreateInboundMetadata();
        var request = new object();
        var forwardedContext = Context.Empty;

        var nextCalled = false;
        await middleware.InvokeAsync(inboundMetadata, request, CaptureNextContext, default);

        Assert.True(nextCalled);
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);

        ValueTask CaptureNextContext(RequestInboundContext context, object forwardedRequest, CancellationToken cancellationToken)
        {
            _ = forwardedRequest;
            _ = cancellationToken;
            nextCalled = true;
            forwardedContext = context;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenKeysUnavailable_ShouldContinueAndKeepHeader()
    {
        var userContext = new UserContext();
        var jwksService = CreateJwksService([]);
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundMetadata = CreateInboundMetadata();
        var request = new object();
        var executionContext = (RequestInboundContext)inboundMetadata
            .WithItem(NOFAbstractionConstants.Transport.Headers.Authorization, "Bearer invalid-token");
        var forwardedContext = Context.Empty;

        var nextCalled = false;
        await middleware.InvokeAsync(executionContext, request, CaptureNextContext, default);

        Assert.True(nextCalled);
        Assert.True(forwardedContext.TryGetItem(NOFAbstractionConstants.Transport.Headers.Authorization, out _));
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);

        ValueTask CaptureNextContext(RequestInboundContext context, object forwardedRequest, CancellationToken cancellationToken)
        {
            _ = forwardedRequest;
            _ = cancellationToken;
            nextCalled = true;
            forwardedContext = context;
            return ValueTask.CompletedTask;
        }
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
        var inboundMetadata = CreateInboundMetadata();
        var request = new object();
        var executionContext = (RequestInboundContext)inboundMetadata
            .WithItem(NOFAbstractionConstants.Transport.Headers.Authorization, "Bearer not-a-jwt");

        var nextCalled = false;
        async Task Act()
        {
            await middleware.InvokeAsync(executionContext, request, (_, _, _) =>
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

    private static RequestInboundContext CreateInboundMetadata()
    {
        var serviceMethod = typeof(object).GetMethod(nameof(ToString))!;
        return new RequestInboundContext
        {
            ServiceType = typeof(object),
            ServiceMethodInfo = serviceMethod,
            HandlerType = typeof(object),
            HandlerMethodInfo = serviceMethod,
            RequestType = typeof(object),
            ResponseType = typeof(Result),
            Metadata = serviceMethod.GetCustomAttributes(inherit: true).ToArray()
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
