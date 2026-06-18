using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Test;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.Extensions;

public sealed class AuthenticationExtensionsTests
{
    private const string SigningKeyEncryptionKey = "jwt-signing-key-passphrase-for-tests";

    [Fact]
    public void JwtId_ShouldExposeStandardJtiClaimType()
    {
        Assert.Equal("jti", ClaimTypes.JwtId);
    }

    [Fact]
    public async Task AddAuthenticationAuthority_WithIssuerOverload_ShouldRegisterAuthorityServices()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        Assert.NotNull(scope.GetRequiredService<ITokenService>());
        Assert.NotNull(scope.GetRequiredService<IJwksService>());
        Assert.NotNull(scope.GetRequiredService<ISigningKeyService>());
        Assert.IsType<LocalJwksService>(scope.GetRequiredService<IJwksService>());
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(ISigningKeyService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IJwksService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.IsType<PersistenceRevokedRefreshTokenRepository>(
        scope.GetRequiredService<IRevokedRefreshTokenRepository>());
        Assert.Contains(scope.Services.GetServices<IHostedService>(), service => service is RevokedRefreshTokenCleanupBackgroundService);
        Assert.Equal("https://issuer.local", scope.GetRequiredService<IOptions<AuthenticationAuthorityOptions>>().Value.Issuer);
    }

    [Fact]
    public async Task AddAuthenticationAuthority_ShouldRegisterPersistentRevokedRefreshTokenRepository()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        var repository = scope.GetRequiredService<IRevokedRefreshTokenRepository>();
        await repository.RevokeAsync("refresh-token-id", TimeSpan.FromMinutes(5));

        Assert.True(await repository.IsRevokedAsync("refresh-token-id"));
        Assert.False(await repository.IsRevokedAsync("unknown-refresh-token-id"));
    }

    [Fact]
    public async Task AddOidcServer_ShouldRegisterProtocolServicesOnly()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(options =>
        {
            options.Issuer = "https://issuer.local/oauth2";
            options.AccessTokenAudience = "nof-tests";
        });

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        Assert.NotNull(scope.GetRequiredService<IOAuthAuthorizationCodeService>());
        Assert.Equal("https://issuer.local/oauth2", scope.GetRequiredService<IOptions<OAuthAuthorizationServerOptions>>().Value.Issuer);
        Assert.Null(scope.Services.GetService<IOAuthAuthorizationHandler>());
        Assert.Null(scope.Services.GetService<IOAuthSubjectService>());
    }

    [Fact]
    public async Task ClientCredentialsGrant_ShouldIssueAccessTokenWithoutRefreshToken()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientStore>(new TestOAuthClientStore())
            .BuildServiceProvider();
        var tokenService = new TestTokenService();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = "client_credentials",
            ClientId = "service-a",
            ClientSecret = "secret-a",
            Scope = "jobs.read jobs.write"
        };

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromClientCredentialsAsync(
            httpContext.Request,
            request,
            services,
            tokenService,
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Null(result.Value.RefreshToken);
        Assert.Equal("jobs.read jobs.write", result.Value.Scope);
        Assert.Equal("jobs-api", tokenService.LastRequest?.Audience);
        Assert.Null(tokenService.LastRequest?.RefreshToken);
        Assert.Contains(tokenService.LastRequest!.AccessClaims!, claim => claim.Type == OAuthClaimTypes.Subject && claim.Value == "service-a");
        Assert.Contains(tokenService.LastRequest.AccessClaims!, claim => claim.Type == "client_id" && claim.Value == "service-a");
    }

    [Fact]
    public async Task AddAuthenticationAuthority_ShouldPersistSigningKeysAcrossHostRestarts()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"nof-jwt-signing-keys-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath}";

        string firstKid;
        string rotatedKid;

        await using (var firstHost = await CreateAuthorityBuilder(connectionString).BuildTestHostAsync())
        {
            using var scope = firstHost.CreateScope();
            var signingKeyService = scope.GetRequiredService<ISigningKeyService>();
            var dbContext = scope.GetRequiredService<NOFDbContext>();

            firstKid = (await signingKeyService.GetCurrentSigningKeyAsync()).Kid;
            var initializedKeys = await EntityFrameworkQueryableExtensions.ToListAsync(dbContext.Set<PersistedSigningKey>().AsNoTracking());

            Assert.Single(initializedKeys, key => key.Status == PersistedSigningKeyStatus.Active);
            Assert.Single(initializedKeys, key => key.Status == PersistedSigningKeyStatus.NextActive);

            await signingKeyService.RotateKeyAsync();
            rotatedKid = (await signingKeyService.GetCurrentSigningKeyAsync()).Kid;
            var rotatedKeys = await EntityFrameworkQueryableExtensions.ToListAsync(dbContext.Set<PersistedSigningKey>().AsNoTracking());

            Assert.NotEqual(firstKid, rotatedKid);
            Assert.Contains(dbContext.Set<PersistedSigningKey>(), key => !string.IsNullOrWhiteSpace(key.PublicKey));
            Assert.Single(rotatedKeys, key => key.Status == PersistedSigningKeyStatus.Active);
            Assert.Single(rotatedKeys, key => key.Status == PersistedSigningKeyStatus.NextActive);
            Assert.Contains(rotatedKeys, key => key.Status == PersistedSigningKeyStatus.Retired && key.Kid == firstKid);
        }

        await using (var secondHost = await CreateAuthorityBuilder(connectionString).BuildTestHostAsync())
        {
            using var scope = secondHost.CreateScope();
            var signingKeyService = scope.GetRequiredService<ISigningKeyService>();
            var dbContext = scope.GetRequiredService<NOFDbContext>();
            var allKeys = await signingKeyService.GetAllKeysAsync();
            var persistedKeys = await EntityFrameworkQueryableExtensions.ToListAsync(dbContext.Set<PersistedSigningKey>().AsNoTracking());

            Assert.Equal(rotatedKid, (await signingKeyService.GetCurrentSigningKeyAsync()).Kid);
            Assert.Contains(allKeys, key => key.Kid == firstKid);
            Assert.Contains(allKeys, key => key.Kid == rotatedKid);
            Assert.Equal(3, allKeys.Length);
            Assert.Single(persistedKeys, key => key.Status == PersistedSigningKeyStatus.Active);
            Assert.Single(persistedKeys, key => key.Status == PersistedSigningKeyStatus.NextActive);
        }

        try
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task AddAuthenticationAuthority_WithoutEncryptionKey_ShouldFallbackToMachineName()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddAuthenticationAuthority(options =>
        {
            options.Issuer = "https://issuer.local";
        });
        builder.UseDbContext<NOFDbContext>()
            .WithConnectionString($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        _ = await scope.GetRequiredService<ISigningKeyService>().GetCurrentSigningKeyAsync();
        var options = scope.GetRequiredService<IOptions<AuthenticationAuthorityOptions>>().Value;

        Assert.Equal(Environment.MachineName, options.SigningKeyEncryptionKey);
    }

    [Fact]
    public async Task AddAuthenticationResourceServer_WithExplicitTokenPropagation_ShouldRegisterSeparately()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtPropagation();
        builder.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServer = "https://auth.local";
            options.RequireHttpsMetadata = true;
            options.Sources.Add(new AuthenticationTokenSourceOptions
            {
                HeaderName = "X-Authorization",
                TokenType = "Token"
            });
        });

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        var resourceOptions = scope.GetRequiredService<IOptions<AuthenticationResourceServerOptions>>().Value;
        Assert.Equal("https://auth.local", resourceOptions.AuthorizationServer);
        Assert.Equal(2, resourceOptions.Sources.Count);
        Assert.Contains(resourceOptions.Sources, source =>
            source.HeaderName == "Authorization" &&
            source.TokenType == "Bearer");
        Assert.Contains(resourceOptions.Sources, source =>
            source.HeaderName == "X-Authorization" &&
            source.TokenType == "Token");
        var jwksService1 = scope.GetRequiredService<IJwksService>();
        var jwksService2 = scope.GetRequiredService<IJwksService>();
        Assert.IsType<HttpJwksService>(jwksService1);
        Assert.IsType<HttpJwksService>(jwksService2);
        Assert.Same(jwksService1, jwksService2);
        Assert.NotNull(scope.GetRequiredService<ResourceServerJwksCacheService>());
    }

    [Fact]
    public async Task AddAuthenticationAuthority_AndResourceServer_ShouldAllowSingletonJwksCacheToRefreshViaScope()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");
        builder.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServer = "https://issuer.local";
            options.Issuer = "https://issuer.local";
        });

        await using var host = await builder.BuildTestHostAsync();
        var cache = host.Services.GetRequiredService<ResourceServerJwksCacheService>();
        using var scope = host.CreateScope();

        Assert.NotNull(cache);
        Assert.IsType<LocalJwksService>(scope.GetRequiredService<IJwksService>());
    }

    [Fact]
    public async Task AddAuthenticationResourceServer_Only_ShouldRegisterInboundResourceServer()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServer = "https://auth.local";
            options.Sources.Add(new AuthenticationTokenSourceOptions
            {
                HeaderName = "X-Authorization",
                TokenType = "Token"
            });
        });

        await using var host = await builder.BuildTestHostAsync();
        Assert.DoesNotContain(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IRequestOutboundMiddleware) &&
            descriptor.ImplementationType?.FullName == "NOF.Hosting.AccessTokenPropagationOutboundMiddleware");
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IRequestInboundMiddleware) &&
            descriptor.ImplementationType == typeof(AuthenticationResourceServerInboundMiddleware));
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(ICommandInboundMiddleware) &&
            descriptor.ImplementationType == typeof(AuthenticationResourceServerInboundMiddleware));
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(INotificationInboundMiddleware) &&
            descriptor.ImplementationType == typeof(AuthenticationResourceServerInboundMiddleware));
        Assert.DoesNotContain(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(ICommandOutboundMiddleware) &&
            descriptor.ImplementationType == typeof(JwtTokenPropagationOutboundMiddleware));
        Assert.DoesNotContain(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(INotificationOutboundMiddleware) &&
            descriptor.ImplementationType == typeof(JwtTokenPropagationOutboundMiddleware));
    }

    [Fact]
    public void AddJwtPropagation_WithInfrastructureDefaults_ShouldRegisterRequestCommandAndNotificationOutboundMiddleware()
    {
        var builder = NOFTestAppBuilder.Create();

        builder.AddJwtPropagation();

        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IRequestOutboundMiddleware) &&
            descriptor.ImplementationType == typeof(Hosting.JwtTokenPropagationOutboundMiddleware));
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(ICommandOutboundMiddleware) &&
            descriptor.ImplementationType == typeof(JwtTokenPropagationOutboundMiddleware));
        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(INotificationOutboundMiddleware) &&
            descriptor.ImplementationType == typeof(JwtTokenPropagationOutboundMiddleware));
    }

    [Fact]
    public async Task AddAuthenticationResourceServer_Only_ShouldNotRegisterAuthorityServicesExplicitly()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServer = "https://auth.local";
        });

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        Assert.Null(scope.Services.GetService<ITokenService>());
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(ITokenService));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(LocalJwksService));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(SigningKeyRotationBackgroundService));
    }

    private static NOFTestAppBuilder CreateAuthorityBuilder(string connectionString)
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddAuthenticationAuthority(options =>
        {
            options.Issuer = "https://issuer.local";
            options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
        });
        builder.UseDbContext<NOFDbContext>()
            .WithConnectionString(connectionString)
            .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

        return builder;
    }

    private sealed class TestOAuthClientStore : IOAuthClientStore
    {
        public ValueTask<OAuthClientCredentialsValidationResult> ValidateClientCredentialsAsync(
            OAuthClientCredentialsValidationRequest request,
            CancellationToken cancellationToken)
        {
            if (request is { ClientId: "service-a", ClientSecret: "secret-a" })
            {
                return ValueTask.FromResult<OAuthClientCredentialsValidationResult>(
                    new OAuthClientCredentialsValidationResult.Success(
                        request.ClientId,
                        request.RequestedScopes,
                        [new("client_id", request.ClientId)]));
            }

            return ValueTask.FromResult<OAuthClientCredentialsValidationResult>(
                new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client credentials are invalid."));
        }
    }

    private sealed class TestTokenService : ITokenService
    {
        public IssueTokenRequest? LastRequest { get; private set; }

        public Task<Result<IssueTokenResponse>> IssueTokenAsync(IssueTokenRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Result.Success(new IssueTokenResponse
            {
                AccessToken = "access-token",
                AccessTokenExpiresAtUtc = DateTime.UtcNow.Add(request.AccessTokenExpiration)
            }));
        }

        public Task<Result<ValidateRefreshTokenResponse>> ValidateRefreshTokenAsync(ValidateRefreshTokenRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Result> RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
