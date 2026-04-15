using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Application;
using Xunit;
using ExecutionContext = NOF.Application.ExecutionContext;

namespace NOF.Infrastructure.Extension.Authorization.Jwt.Tests.Middlewares;

public sealed class JwtResourceServerInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenAuthorizationHeaderMissing_ShouldContinueWithoutValidation()
    {
        var userContext = new UserContext();
        var jwksProvider = new FakeJwksProvider([]);
        var executionContext = new ExecutionContext();
        var middleware = CreateMiddleware(userContext, jwksProvider, executionContext);

        var nextCalled = false;
        await middleware.InvokeAsync(CreateInboundContext(), _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(nextCalled);
        Assert.Equal(0, jwksProvider.CallCount);
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_WhenKeysUnavailable_ShouldContinueAndKeepHeader()
    {
        var userContext = new UserContext();
        var jwksProvider = new FakeJwksProvider([]);
        var executionContext = new ExecutionContext
        {
            [NOFAbstractionConstants.Transport.Headers.Authorization] = "Bearer invalid-token"
        };
        var middleware = CreateMiddleware(userContext, jwksProvider, executionContext);

        var nextCalled = false;
        await middleware.InvokeAsync(CreateInboundContext(), _ =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);
        Assert.True(nextCalled);
        Assert.Equal(1, jwksProvider.CallCount);
        Assert.True(executionContext.ContainsKey(NOFAbstractionConstants.Transport.Headers.Authorization));
        Assert.NotNull(userContext.User);
        Assert.False(userContext.User.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_WhenTokenInvalid_ShouldContinueWithoutThrowing()
    {
        var userContext = new UserContext();
        var jwksProvider = new FakeJwksProvider([new JsonWebKey
        {
            Kid = "kid-1",
            Kty = "RSA",
            Use = "sig",
            N = "abc",
            E = "AQAB"
        }]);
        var executionContext = new ExecutionContext
        {
            [NOFAbstractionConstants.Transport.Headers.Authorization] = "Bearer not-a-jwt"
        };
        var middleware = CreateMiddleware(userContext, jwksProvider, executionContext);

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
        IJwksProvider jwksProvider,
        IExecutionContext executionContext)
    {
        return new JwtResourceServerInboundMiddleware(
            userContext,
            jwksProvider,
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

    private static InboundContext CreateInboundContext()
    {
        return new InboundContext
        {
            Message = new object(),
            Services = new ServiceCollection().BuildServiceProvider(),
            Attributes = new List<Attribute>(),
            Metadatas = new Dictionary<string, object?> { { "HandlerType", typeof(object) } }
        };
    }

    private sealed class FakeJwksProvider(IReadOnlyCollection<SecurityKey> keys) : IJwksProvider
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult((IReadOnlyList<SecurityKey>)keys.ToList());
        }

        public Task<IReadOnlyList<SecurityKey>> RefreshAsync(CancellationToken cancellationToken = default)
        {
            return GetSecurityKeysAsync(cancellationToken);
        }
    }
}
