using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting.Extension.Authorization.Jwt;
using NOF.Test;
using System.Security.Claims;
using Xunit;

namespace NOF.Infrastructure.Extension.Authorization.Jwt.Tests.Extensions;

public sealed class JwtAuthorizationExtensionsTests
{
    private const string SigningKeyEncryptionKey = "jwt-signing-key-passphrase-for-tests";

    [Fact]
    public void JwtId_ShouldExposeStandardJtiClaimType()
    {
        Assert.Equal("jti", ClaimTypes.JwtId);
    }

    [Fact]
    public async Task AddJwtAuthority_WithIssuerOverload_ShouldRegisterAuthorityServices()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        Assert.NotNull(scope.GetRequiredService<JwtAuthorityService>());
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
        Assert.Equal("https://issuer.local", scope.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value.Issuer);
    }

    [Fact]
    public async Task AddJwtAuthority_ShouldRegisterPersistentRevokedRefreshTokenRepository()
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
    public async Task AddJwtAuthority_ShouldPersistSigningKeysAcrossHostRestarts()
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
            var initializedKeys = await dbContext.Set<PersistedSigningKey>().AsNoTracking().ToListAsync();

            Assert.Single(initializedKeys, key => key.Status == PersistedSigningKeyStatus.Active);
            Assert.Single(initializedKeys, key => key.Status == PersistedSigningKeyStatus.NextActive);

            await signingKeyService.RotateKeyAsync();
            rotatedKid = (await signingKeyService.GetCurrentSigningKeyAsync()).Kid;
            var rotatedKeys = await dbContext.Set<PersistedSigningKey>().AsNoTracking().ToListAsync();

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
            var persistedKeys = await dbContext.Set<PersistedSigningKey>().AsNoTracking().ToListAsync();

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
    public async Task AddJwtAuthority_WithoutEncryptionKey_ShouldFallbackToMachineName()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtAuthority(options =>
        {
            options.Issuer = "https://issuer.local";
        });
        builder.UseDbContext<NOFDbContext>()
            .WithConnectionString($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        _ = await scope.GetRequiredService<ISigningKeyService>().GetCurrentSigningKeyAsync();
        var options = scope.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value;

        Assert.Equal(Environment.MachineName, options.SigningKeyEncryptionKey);
    }

    [Fact]
    public async Task AddJwtResourceServer_WithExplicitTokenPropagation_ShouldRegisterSeparately()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtTokenPropagation();
        builder.AddJwtResourceServer(options =>
        {
            options.JwksEndpoint = "https://auth.local/.well-known/jwks.json";
            options.RequireHttpsMetadata = true;
            options.Sources.Add(new JwtResourceServerTokenSourceOptions
            {
                HeaderName = "X-Authorization",
                TokenType = "Token"
            });
        });

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        var resourceOptions = scope.GetRequiredService<IOptions<JwtResourceServerOptions>>().Value;
        Assert.Equal("https://auth.local/.well-known/jwks.json", resourceOptions.JwksEndpoint);
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
    public async Task AddJwtAuthority_AndResourceServer_ShouldAllowSingletonJwksCacheToRefreshViaScope()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");
        builder.AddJwtResourceServer(options =>
        {
            options.JwksEndpoint = "https://issuer.local/.well-known/jwks.json";
            options.Issuer = "https://issuer.local";
        });

        await using var host = await builder.BuildTestHostAsync();
        var cache = host.Services.GetRequiredService<ResourceServerJwksCacheService>();
        using var scope = host.CreateScope();

        Assert.NotNull(cache);
        Assert.IsType<LocalJwksService>(scope.GetRequiredService<IJwksService>());
    }

    [Fact]
    public async Task AddJwtResourceServer_Only_ShouldNotRegisterTokenPropagation()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtResourceServer(options =>
        {
            options.JwksEndpoint = "https://auth.local/.well-known/jwks.json";
            options.Sources.Add(new JwtResourceServerTokenSourceOptions
            {
                HeaderName = "X-Authorization",
                TokenType = "Token"
            });
        });

        await using var host = await builder.BuildTestHostAsync();
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(JwtTokenPropagationOutboundMiddleware));
    }

    [Fact]
    public async Task AddJwtResourceServer_Only_ShouldNotRegisterAuthorityServicesExplicitly()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtResourceServer(options =>
        {
            options.JwksEndpoint = "https://auth.local/.well-known/jwks.json";
        });

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        Assert.Null(scope.Services.GetService<JwtAuthorityService>());
        Assert.Null(scope.Services.GetService<JwtAuthorityService.GenerateJwtToken>());
        Assert.Null(scope.Services.GetService<JwtAuthorityService.ValidateJwtRefreshToken>());
        Assert.Null(scope.Services.GetService<JwtAuthorityService.RevokeJwtRefreshToken>());
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(GenerateJwtTokenHandler));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(ValidateJwtRefreshTokenHandler));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(RevokeJwtRefreshTokenHandler));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(IJwtAuthorityServiceClient));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(LocalJwtAuthorityServiceClient));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(LocalJwksService));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ImplementationType == typeof(JwtKeyRotationBackgroundService));
    }

    private static NOFTestAppBuilder CreateAuthorityBuilder(string connectionString)
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtAuthority(options =>
        {
            options.Issuer = "https://issuer.local";
            options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
        });
        builder.UseDbContext<NOFDbContext>()
            .WithConnectionString(connectionString)
            .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

        return builder;
    }
}
