using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.Middlewares;

public sealed class AuthenticationResourceServerInboundMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenAuthorizationHeaderMissing_ShouldContinueWithoutValidation()
    {
        var userContext = new UserContext();
        var jwksService = CreateJwksService([]);
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundContext = CreateInboundContext();
        var request = new object();
        var forwardedContext = Context.Empty;

        var nextCalled = false;
        await middleware.InvokeAsync(inboundContext, request, CaptureNextContext, default);

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
        var inboundContext = CreateInboundContext();
        var request = new object();
        var executionContext = (RequestInboundContext)inboundContext
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
        var inboundContext = CreateInboundContext();
        var request = new object();
        var executionContext = (RequestInboundContext)inboundContext
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

    [Fact]
    public async Task InvokeAsync_WhenIssuerNotConfigured_ShouldValidateTokenWithDiscoveredIssuer()
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
        var token = CreateToken(key.Key, "https://auth.local");
        var jwksService = CreateJwksService([key], "https://auth.local");
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundContext = (RequestInboundContext)CreateInboundContext()
            .WithItem(NOFAbstractionConstants.Transport.Headers.Authorization, $"Bearer {token}");

        var nextCalled = false;
        await middleware.InvokeAsync(inboundContext, new object(), (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.CompletedTask;
        }, default);

        Assert.True(nextCalled);
        Assert.True(userContext.User.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_WhenTokenContainsScope_ShouldMapScopeToPermission()
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
        var token = CreateToken(
            key.Key,
            "https://auth.local",
            [new Claim(JwtRegisteredClaimNames.Sub, "user-1"), new Claim("scope", "orders.read orders.write")]);
        var jwksService = CreateJwksService([key], "https://auth.local");
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundContext = (RequestInboundContext)CreateInboundContext()
            .WithItem(NOFAbstractionConstants.Transport.Headers.Authorization, $"Bearer {token}");

        await middleware.InvokeAsync(inboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.True(userContext.User.IsAuthenticated);
        Assert.Contains("orders.read", userContext.User.Permissions);
        Assert.Contains("orders.write", userContext.User.Permissions);
    }

    [Fact]
    public async Task InvokeAsync_WhenTokenContainsStandardNameClaim_ShouldPopulateUserName()
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
        var token = CreateToken(
            key.Key,
            "https://auth.local",
            [new Claim(JwtRegisteredClaimNames.Sub, "user-1"), new Claim("name", "Alice")]);
        var jwksService = CreateJwksService([key], "https://auth.local");
        var middleware = CreateMiddleware(userContext, jwksService);
        var inboundContext = (RequestInboundContext)CreateInboundContext()
            .WithItem(NOFAbstractionConstants.Transport.Headers.Authorization, $"Bearer {token}");

        await middleware.InvokeAsync(inboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.Equal("Alice", userContext.User.Name);
    }

    [Fact]
    public async Task InvokeAsync_WhenCustomResolverUsesOtherClaims_ShouldMapPermission()
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
        var token = CreateToken(
            key.Key,
            "https://auth.local",
            [new Claim(JwtRegisteredClaimNames.Sub, "user-1"), new Claim(ClaimTypes.Role, "ops-admin")]);
        var jwksService = CreateJwksService([key], "https://auth.local");
        var middleware = CreateMiddleware(userContext, jwksService, new RolePermissionResolver());
        var inboundContext = (RequestInboundContext)CreateInboundContext()
            .WithItem(NOFAbstractionConstants.Transport.Headers.Authorization, $"Bearer {token}");

        await middleware.InvokeAsync(inboundContext, new object(), (_, _, _) => ValueTask.CompletedTask, default);

        Assert.True(userContext.User.IsAuthenticated);
        Assert.Contains("ops.full", userContext.User.Permissions);
    }

    private static AuthenticationResourceServerInboundMiddleware CreateMiddleware(
        IUserContext userContext,
        ResourceServerJwksCacheService jwksCacheService,
        IPermissionResolver? permissionResolver = null)
    {
        return new AuthenticationResourceServerInboundMiddleware(
            userContext,
            jwksCacheService,
            permissionResolver ?? new ScopePermissionResolver(),
            Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions
            {
                AuthorizationServer = "https://auth.local",
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

    private static ResourceServerJwksCacheService CreateJwksService(IReadOnlyList<ManagedSigningKey> keys, string? issuer = null)
    {
        var signingKeyService = new FakeSigningKeyService([.. keys]);
        var services = new ServiceCollection();
        services.AddScoped<IJwksService>(_ => new LocalJwksService(signingKeyService));
        if (!string.IsNullOrWhiteSpace(issuer))
        {
            services.AddScoped<IAuthorizationServerMetadataService>(_ => new FakeAuthorizationServerMetadataService(issuer));
        }

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new ResourceServerJwksCacheService(
            scopeFactory,
            Microsoft.Extensions.Options.Options.Create(new AuthenticationResourceServerOptions()),
            TimeProvider.System);
    }

    private static string CreateToken(SecurityKey key, string issuer, IReadOnlyList<Claim>? claims = null)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null,
            claims: claims ?? [new Claim(JwtRegisteredClaimNames.Sub, "user-1")],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static RequestInboundContext CreateInboundContext()
    {
        var serviceMethod = typeof(object).GetMethod(nameof(ToString))!;
        return new RequestInboundContext
        {
            ServiceType = typeof(object),
            ServiceMethodInfo = serviceMethod,
            HandlerType = typeof(object),
            HandlerMethodInfo = serviceMethod,
            RequestType = typeof(object),
            ResponseType = typeof(Result)
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

    private sealed class FakeAuthorizationServerMetadataService(string issuer) : IAuthorizationServerMetadataService
    {
        public Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<OAuthAuthorizationServerMetadataDocument?>(new OAuthAuthorizationServerMetadataDocument
            {
                Issuer = issuer,
                JwksUri = $"{issuer}/.well-known/jwks.json"
            });
        }
    }

    private sealed class RolePermissionResolver : IPermissionResolver
    {
        public IReadOnlyCollection<string> ResolvePermissions(IReadOnlyCollection<Claim> claims)
        {
            return claims.Any(static claim => claim.Type == ClaimTypes.Role && claim.Value == "ops-admin")
                ? ["ops.full"]
                : [];
        }
    }
}
