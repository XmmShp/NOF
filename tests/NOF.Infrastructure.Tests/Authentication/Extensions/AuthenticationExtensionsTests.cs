using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Test;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    public async Task AddOidcServer_WithIssuerOverload_ShouldRegisterAuthorityServices()
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
        Assert.Equal("https://issuer.local", scope.GetRequiredService<IOptions<OAuthAuthorizationServerOptions>>().Value.Issuer);
    }

    [Fact]
    public async Task AddOidcServer_ShouldRegisterPersistentRevokedRefreshTokenRepository()
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
    public async Task AddOidcServer_ShouldMapOidcEndpointsAutomatically()
    {
        var builder = NOFWebApplicationBuilder.Create([]);
        builder.AddOidcServer(options =>
        {
            options.Issuer = "https://issuer.local/oauth2";
        });

        await using var app = await builder.BuildAsync();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Contains("/oauth2/token", routes);
        Assert.Contains("/oauth2/revoke", routes);
        Assert.Contains("/oauth2/introspect", routes);
        Assert.Contains("/oauth2/authorize", routes);
        Assert.Contains("/.well-known/openid-configuration", routes);
    }

    [Fact]
    public void OAuthAuthorizationServerOptions_ShouldSupportOfflineAccessByDefault()
    {
        var options = new OAuthAuthorizationServerOptions();

        Assert.Contains(OAuthScope.OfflineAccess, options.ScopesSupported);
    }

    [Fact]
    public void OAuthAuthorizationServerOptions_ShouldSupportEmailVerifiedClaimByDefault()
    {
        var options = new OAuthAuthorizationServerOptions();

        Assert.Contains(OAuthClaimTypes.EmailVerified, options.ClaimsSupported);
    }

    [Fact]
    public void OAuthAuthorizationServerOptions_ShouldSupportClientIdAndEntitlementsClaimsByDefault()
    {
        var options = new OAuthAuthorizationServerOptions();

        Assert.Contains(OAuthClaimTypes.ClientId, options.ClaimsSupported);
        Assert.Contains(OAuthClaimTypes.Entitlements, options.ClaimsSupported);
    }

    [Fact]
    public async Task AddOidcServer_AddPublicClient_ShouldEnsureClientExistsOnInitialize()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(options =>
        {
            options.Issuer = "https://issuer.local/oauth2";
            options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
        })
        .AddPublicClient(
            "bootstrap-public-client",
            ["openid", "profile", "jobs.read"],
            displayName: "Bootstrap Public Client",
            redirectUris: ["https://public.local/callback"]);
        builder.UseDbContext<NOFDbContext>()
            .WithTenantMode(TenantMode.DatabasePerTenant)
            .WithConnectionString($"Data Source=nof-bootstrap-public-client-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString))
            .MigrateOnInitialize();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var clientService = scope.GetRequiredService<IOAuthClientManagementService>();

        var client = await clientService.GetAsync("bootstrap-public-client");

        Assert.True(client.IsSuccess, client.Message);
        Assert.Equal(OAuthClientType.Public, client.Value.ClientType);
        Assert.Equal("Bootstrap Public Client", client.Value.DisplayName);
        Assert.Equal(["jobs.read", "openid", "profile"], client.Value.AllowedScopes.OrderBy(static scope => scope, StringComparer.Ordinal).ToArray());
        Assert.Equal(["https://public.local/callback"], client.Value.RedirectUris);
    }

    [Fact]
    public async Task AddOidcServer_AddPublicClient_ShouldNotOverwriteExistingClient()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"nof-bootstrap-existing-public-client-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath}";

        try
        {
            await using (var firstHost = await CreateAuthorityBuilder(connectionString).BuildTestHostAsync())
            {
                using var scope = firstHost.CreateScope();
                var clientService = scope.GetRequiredService<IOAuthClientManagementService>();

                var createResult = await clientService.CreateAsync(new CreateOAuthClientRequest
                {
                    ClientId = "bootstrap-public-client",
                    DisplayName = "Existing Public Client",
                    AllowedScopes = ["existing.scope"],
                    RedirectUris = ["https://existing.local/callback"],
                    ClientType = OAuthClientType.Public,
                    IsEnabled = false
                });

                Assert.True(createResult.IsSuccess, createResult.Message);
            }

            var secondBuilder = NOFTestAppBuilder.Create();
            secondBuilder.AddOidcServer(options =>
            {
                options.Issuer = "https://issuer.local";
                options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
            })
            .AddPublicClient(
                "bootstrap-public-client",
                ["openid", "profile", "jobs.read"],
                displayName: "Bootstrap Public Client",
                redirectUris: ["https://public.local/callback"]);
            secondBuilder.UseDbContext<NOFDbContext>()
                .WithConnectionString(connectionString)
                .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

            await using var secondHost = await secondBuilder.BuildTestHostAsync();
            using var secondScope = secondHost.CreateScope();
            var secondClientService = secondScope.GetRequiredService<IOAuthClientManagementService>();

            var existingClient = await secondClientService.GetAsync("bootstrap-public-client");

            Assert.True(existingClient.IsSuccess, existingClient.Message);
            Assert.Equal("Existing Public Client", existingClient.Value.DisplayName);
            Assert.Equal(["existing.scope"], existingClient.Value.AllowedScopes.OrderBy(static scope => scope, StringComparer.Ordinal).ToArray());
            Assert.Equal(["https://existing.local/callback"], existingClient.Value.RedirectUris);
            Assert.False(existingClient.Value.IsEnabled);
        }
        finally
        {
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
    }

    [Fact]
    public async Task AddOidcServer_AddConfidentialClient_ShouldEnsureClientExistsOnInitialize()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(options =>
        {
            options.Issuer = "https://issuer.local/oauth2";
            options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
        })
        .AddConfidentialClient(
            "bootstrap-confidential-client",
            "bootstrap-secret",
            ["jobs.read", "jobs.write"],
            displayName: "Bootstrap Confidential Client",
            redirectUris: ["https://confidential.local/callback"]);
        builder.UseDbContext<NOFDbContext>()
            .WithTenantMode(TenantMode.DatabasePerTenant)
            .WithConnectionString($"Data Source=nof-bootstrap-confidential-client-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, connectionString) => optionsBuilder.UseSqlite(connectionString))
            .MigrateOnInitialize();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var clientService = scope.GetRequiredService<IOAuthClientManagementService>();

        var client = await clientService.GetAsync("bootstrap-confidential-client");
        var validation = await clientService.ValidateClientCredentialsAsync(
            new OAuthClientCredentialsValidationRequest(
                "bootstrap-confidential-client",
                "bootstrap-secret",
                new HashSet<string>(["jobs.read"], StringComparer.Ordinal),
                "client_secret_post"),
            CancellationToken.None);

        Assert.True(client.IsSuccess, client.Message);
        Assert.Equal(OAuthClientType.Confidential, client.Value.ClientType);
        Assert.Equal("Bootstrap Confidential Client", client.Value.DisplayName);
        Assert.Equal(["jobs.read", "jobs.write"], client.Value.AllowedScopes.OrderBy(static scope => scope, StringComparer.Ordinal).ToArray());
        Assert.Equal(["https://confidential.local/callback"], client.Value.RedirectUris);
        Assert.IsType<OAuthClientCredentialsValidationResult.Success>(validation);
    }

    [Fact]
    public async Task ClientCredentialsGrant_ShouldIssueAccessTokenWithoutRefreshToken()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
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
        Assert.Contains(tokenService.LastRequest!.AccessClaims!, claim => claim.Type == OAuthClaimTypes.Subject && claim.Value == "client:service-a");
        Assert.Equal("service-a", tokenService.LastRequest.ClientId);
    }

    [Fact]
    public async Task ValidateClientAuthenticationAsync_ShouldAllowAuthorizationCodeRequestWithoutClientSecret_WhenPkceIsUsed()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.AuthorizationCode,
            ClientId = "public-app",
            Code = "code-1",
            CodeVerifier = "pkce-code-verifier",
            RedirectUri = "https://app.local/callback"
        };

        var error = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateClientAuthenticationAsync(
            httpContext.Request,
            request,
            services,
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateClientAuthenticationAsync_ShouldRejectAuthorizationCodeRequestWithoutClientSecret_WhenPkceIsMissing()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.AuthorizationCode,
            ClientId = "service-a",
            Code = "code-1",
            RedirectUri = "https://app.local/callback"
        };

        var error = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateClientAuthenticationAsync(
            httpContext.Request,
            request,
            services,
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal("invalid_client", error.Error);
        Assert.Equal("client_secret is required.", error.ErrorDescription);
    }

    [Fact]
    public async Task ValidateClientAuthenticationAsync_ShouldAllowPublicRefreshTokenRequestWithoutClientSecret()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.RefreshToken,
            ClientId = "public-app",
            RefreshToken = "refresh-token"
        };

        var error = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateClientAuthenticationAsync(
            httpContext.Request,
            request,
            services,
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateClientAuthenticationAsync_ShouldRejectPublicClientCredentialsGrant()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.ClientCredentials,
            ClientId = "public-app"
        };

        var error = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateClientAuthenticationAsync(
            httpContext.Request,
            request,
            services,
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal("invalid_client", error.Error);
        Assert.Equal("public client authentication is invalid for this grant type.", error.ErrorDescription);
    }

    [Fact]
    public async Task ValidateClientAuthenticationAsync_ShouldAcceptPublicClientTokenExchangeGrant()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.TokenExchange,
            ClientId = "public-app"
        };

        var error = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateClientAuthenticationAsync(
            httpContext.Request,
            request,
            services,
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateClientAuthenticationAsync_ShouldAcceptBasicClientCredentialsAndNormalizeRequest()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        httpContext.Request.Headers.Authorization =
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("service-a:secret-a"));
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.RefreshToken,
            RefreshToken = "refresh-token"
        };

        Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ApplyResolvedClientCredentials(httpContext.Request, request);
        var error = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateClientAuthenticationAsync(
            httpContext.Request,
            request,
            services,
            CancellationToken.None);

        Assert.Null(error);
        Assert.Equal("service-a", request.ClientId);
        Assert.Equal("secret-a", request.ClientSecret);
    }

    [Fact]
    public async Task TokenFromRefreshTokenAsync_ShouldRejectMismatchedClientId()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.RefreshToken,
            ClientId = "service-a",
            ClientSecret = "secret-a",
            RefreshToken = "refresh-token"
        };
        var tokenService = new TestTokenService
        {
            ValidateRefreshTokenResult = Result.Success(new ValidateRefreshTokenResponse
            {
                TokenId = "refresh-token-id",
                Claims =
                [
                    new TokenClaim(OAuthClaimTypes.Subject, "user-1"),
                    new TokenClaim(OAuthClaimTypes.Scope, "jobs.read"),
                    new TokenClaim(OAuthClaimTypes.ClientId, "other-client")
                ]
            })
        };

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromRefreshTokenAsync(
            httpContext.Request,
            request,
            services,
            new TestOAuthSubjectService(),
            tokenService,
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                RefreshTokenExpiration = TimeSpan.FromDays(7),
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_grant", result.ErrorCode);
        Assert.Equal("refresh token client does not match.", result.Message);
    }

    [Fact]
    public async Task TokenFromRefreshTokenAsync_ShouldIssueNewRefreshTokenBoundToClientId()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.RefreshToken,
            ClientId = "service-a",
            ClientSecret = "secret-a",
            RefreshToken = "refresh-token"
        };
        var tokenService = new TestTokenService
        {
            ValidateRefreshTokenResult = Result.Success(new ValidateRefreshTokenResponse
            {
                TokenId = "refresh-token-id",
                Claims =
                [
                    new TokenClaim(OAuthClaimTypes.Subject, "user-1"),
                    new TokenClaim(OAuthClaimTypes.Scope, "jobs.read offline_access"),
                    new TokenClaim(OAuthClaimTypes.ClientId, "service-a")
                ]
            })
        };

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromRefreshTokenAsync(
            httpContext.Request,
            request,
            services,
            new TestOAuthSubjectService(),
            tokenService,
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                RefreshTokenExpiration = TimeSpan.FromDays(7),
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(tokenService.LastRequest?.RefreshToken);
        Assert.Contains(tokenService.LastRequest!.RefreshToken!.Claims!, claim => claim.Type == OAuthClaimTypes.ClientId && claim.Value == "service-a");
        Assert.Equal("refresh-token-id", tokenService.LastRevokeRequest?.TokenId);
    }

    [Fact]
    public async Task TokenFromRefreshTokenAsync_WithoutOfflineAccess_ShouldNotIssueNewRefreshToken()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.RefreshToken,
            ClientId = "service-a",
            ClientSecret = "secret-a",
            RefreshToken = "refresh-token"
        };
        var tokenService = new TestTokenService
        {
            ValidateRefreshTokenResult = Result.Success(new ValidateRefreshTokenResponse
            {
                TokenId = "refresh-token-id",
                Claims =
                [
                    new TokenClaim(OAuthClaimTypes.Subject, "user-1"),
                    new TokenClaim(OAuthClaimTypes.Scope, "jobs.read"),
                    new TokenClaim(OAuthClaimTypes.ClientId, "service-a")
                ]
            })
        };

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromRefreshTokenAsync(
            httpContext.Request,
            request,
            services,
            new TestOAuthSubjectService(),
            tokenService,
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                RefreshTokenExpiration = TimeSpan.FromDays(7),
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(tokenService.LastRequest?.RefreshToken);
        Assert.Null(result.Value.RefreshToken);
    }

    [Fact]
    public async Task TokenFromRefreshTokenAsync_ShouldAllowPublicClientWithoutClientSecret()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.RefreshToken,
            ClientId = "public-app",
            RefreshToken = "refresh-token"
        };
        var tokenService = new TestTokenService
        {
            ValidateRefreshTokenResult = Result.Success(new ValidateRefreshTokenResponse
            {
                TokenId = "refresh-token-id",
                Claims =
                [
                    new TokenClaim(OAuthClaimTypes.Subject, "user-1"),
                    new TokenClaim(OAuthClaimTypes.Scope, "jobs.read offline_access"),
                    new TokenClaim(OAuthClaimTypes.ClientId, "public-app")
                ]
            })
        };

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromRefreshTokenAsync(
            httpContext.Request,
            request,
            services,
            new TestOAuthSubjectService(),
            tokenService,
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                RefreshTokenExpiration = TimeSpan.FromDays(7),
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(tokenService.LastRequest?.RefreshToken);
        Assert.Contains(tokenService.LastRequest!.RefreshToken!.Claims!, claim => claim.Type == OAuthClaimTypes.ClientId && claim.Value == "public-app");
    }

    [Theory]
    [InlineData("openid profile", false)]
    [InlineData("openid profile offline_access", true)]
    [InlineData("offline_access", true)]
    public void ShouldIssueRefreshToken_ShouldFollowOfflineAccessScope(string scope, bool expected)
    {
        Assert.Equal(expected, Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ShouldIssueRefreshToken(scope));
    }

    [Fact]
    public async Task AddOidcServer_OAuthClientService_ShouldCreatePublicClientWithoutSecret()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-oauth-client-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var service = scope.GetRequiredService<IOAuthClientManagementService>();

        var result = await service.CreateAsync(new CreateOAuthClientRequest
        {
            ClientId = "public-app",
            DisplayName = "Public App",
            ClientType = OAuthClientType.Public,
            AllowedScopes = ["jobs.read"],
            RedirectUris = ["https://app.local/callback"]
        });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(OAuthClientType.Public, result.Value.Client.ClientType);
        Assert.Null(result.Value.ClientSecret);

        var rotateResult = await service.RotateSecretAsync("public-app");
        Assert.False(rotateResult.IsSuccess);
        Assert.Equal("invalid_operation", rotateResult.ErrorCode);
    }

    [Fact]
    public async Task AddOidcServer_OAuthClientService_ShouldCreateConfidentialClientWithProvidedSecret()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-oauth-client-confidential-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var service = scope.GetRequiredService<IOAuthClientManagementService>();

        var result = await service.CreateAsync(new CreateOAuthClientRequest
        {
            ClientId = "confidential-app",
            ClientSecret = "provided-secret",
            DisplayName = "Confidential App",
            ClientType = OAuthClientType.Confidential,
            AllowedScopes = ["jobs.read"],
            RedirectUris = ["https://confidential.local/callback"]
        });
        var validation = await service.ValidateClientCredentialsAsync(
            new OAuthClientCredentialsValidationRequest(
                "confidential-app",
                "provided-secret",
                new HashSet<string>(["jobs.read"], StringComparer.Ordinal),
                "client_secret_post"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("provided-secret", result.Value.ClientSecret);
        Assert.IsType<OAuthClientCredentialsValidationResult.Success>(validation);
    }

    [Fact]
    public async Task AddOidcServer_OAuthClientService_ShouldPersistRedirectUris()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-oauth-client-redirect-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var service = scope.GetRequiredService<IOAuthClientManagementService>();

        var result = await service.CreateAsync(new CreateOAuthClientRequest
        {
            ClientId = "redirect-client",
            DisplayName = "Redirect Client",
            ClientType = OAuthClientType.Public,
            RedirectUris = ["https://app.local/callback", "https://app.local/signout"]
        });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(
            ["https://app.local/callback", "https://app.local/signout"],
            result.Value.Client.RedirectUris.OrderBy(static uri => uri, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task AddOidcServer_OAuthClientService_ShouldTreatBlankRedirectUrisAsEmptyList()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-oauth-client-blank-redirect-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var dbContext = scope.GetRequiredService<NOFDbContext>();
        var service = scope.GetRequiredService<IOAuthClientManagementService>();
        var now = DateTime.UtcNow;

        await dbContext.Set<OAuthClient>().AddAsync(new OAuthClient
        {
            ClientId = "blank-redirect-client",
            DisplayName = "Blank Redirect Client",
            SecretHash = string.Empty,
            SecretSalt = string.Empty,
            AllowedScopes = "[]",
            RedirectUris = string.Empty,
            AccessTokenClaims = "[]",
            ClientType = OAuthClientType.Public,
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetAsync("blank-redirect-client");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(result.Value.RedirectUris);
    }

    [Fact]
    public async Task ValidateAuthorizationRequestAsync_ShouldRejectUnregisteredRedirectUri()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();

        var request = new OAuthAuthorizationRequest(
            ResponseType: "code",
            ClientId: "public-app",
            RedirectUri: "https://evil.local/callback",
            Scope: "jobs.read",
            State: "state-1",
            Nonce: null,
            CodeChallenge: null,
            CodeChallengeMethod: null);

        var (_, error, allowRedirect) = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateAuthorizationRequestAsync(
            services,
            request,
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Error);
        Assert.Equal("redirect_uri is not registered for this client.", error.ErrorDescription);
        Assert.False(allowRedirect);
    }

    [Fact]
    public async Task ValidateAuthorizationRequestAsync_ShouldAllowMissingRedirectUri_WhenClientHasSingleRegisteredRedirectUri()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();

        var request = new OAuthAuthorizationRequest(
            ResponseType: "code",
            ClientId: "public-app",
            RedirectUri: string.Empty,
            Scope: "jobs.read",
            State: "state-1",
            Nonce: null,
            CodeChallenge: null,
            CodeChallengeMethod: null);

        var (resolvedRequest, error, allowRedirect) = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateAuthorizationRequestAsync(
            services,
            request,
            CancellationToken.None);

        Assert.Null(error);
        Assert.True(allowRedirect);
        Assert.Equal("https://app.local/callback", resolvedRequest.RedirectUri);
    }

    [Fact]
    public async Task ValidateAuthorizationRequestAsync_ShouldRequireRedirectUri_WhenClientHasMultipleRegisteredRedirectUris()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .BuildServiceProvider();

        var request = new OAuthAuthorizationRequest(
            ResponseType: "code",
            ClientId: "multi-redirect-app",
            RedirectUri: string.Empty,
            Scope: "jobs.read",
            State: "state-1",
            Nonce: null,
            CodeChallenge: null,
            CodeChallengeMethod: null);

        var (_, error, allowRedirect) = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.ValidateAuthorizationRequestAsync(
            services,
            request,
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal("invalid_request", error.Error);
        Assert.Equal("redirect_uri is required when the client does not have exactly one registered redirect URI.", error.ErrorDescription);
        Assert.False(allowRedirect);
    }

    [Fact]
    public async Task TokenExchangeGrant_ShouldIntersectRequestedScopesWithSubjectTokenAndActorToken()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .AddSingleton<IOAuthTokenExchangeHandler, DefaultOAuthTokenExchangeHandler>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.TokenExchange,
            ClientId = "service-a",
            ClientSecret = "secret-a",
            SubjectToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "user-1"), new Claim(OAuthClaimTypes.Scope, "jobs.read jobs.write jobs.delete")]),
            ActorToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "client:order-service"), new Claim(OAuthClaimTypes.Actor, """{"sub":"client:web-app"}"""), new Claim(OAuthClaimTypes.Scope, "jobs.read jobs.audit")]),
            SubjectTokenType = OAuthTokenTypes.AccessToken,
            ActorTokenType = OAuthTokenTypes.AccessToken,
            RequestedTokenType = OAuthTokenTypes.AccessToken,
            Scope = "jobs.read jobs.write jobs.audit"
        };
        var subjectService = new TestOAuthSubjectService();
        var tokenService = new TestTokenService();

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromTokenExchangeAsync(
            httpContext.Request,
            request,
            services,
            subjectService,
            tokenService,
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("jobs.read", result.Value.Scope);
        Assert.Null(result.Value.RefreshToken);
        Assert.Equal(["jobs.read"], subjectService.LastRequestedScopes.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
        Assert.NotNull(tokenService.LastRequest);
        Assert.Null(tokenService.LastRequest!.RefreshToken);
        Assert.Contains(tokenService.LastRequest.AccessClaims!, claim => claim.Type == OAuthClaimTypes.Scope && claim.Value == "jobs.read");
        var actClaim = Assert.Single(tokenService.LastRequest.AccessClaims!, static claim => claim.Type == OAuthClaimTypes.Actor);
        Assert.Equal(Microsoft.IdentityModel.JsonWebTokens.JsonClaimValueTypes.Json, actClaim.ValueType);
        using var actDocument = JsonDocument.Parse(actClaim.Value!);
        Assert.Equal("client:order-service", actDocument.RootElement.GetProperty(OAuthClaimTypes.Subject).GetString());
        Assert.Equal("client:web-app", actDocument.RootElement.GetProperty(OAuthClaimTypes.Actor).GetProperty(OAuthClaimTypes.Subject).GetString());
    }

    [Fact]
    public async Task TokenExchangeGrant_ShouldRequireClientAuthentication()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .AddSingleton<IOAuthTokenExchangeHandler, DefaultOAuthTokenExchangeHandler>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.TokenExchange,
            SubjectToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "user-1"), new Claim(OAuthClaimTypes.Scope, "jobs.read")]),
            ActorToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "client:order-service"), new Claim(OAuthClaimTypes.Scope, "jobs.read")]),
            SubjectTokenType = OAuthTokenTypes.AccessToken,
            ActorTokenType = OAuthTokenTypes.AccessToken,
            RequestedTokenType = OAuthTokenTypes.AccessToken,
            Scope = "jobs.read"
        };

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromTokenExchangeAsync(
            httpContext.Request,
            request,
            services,
            new TestOAuthSubjectService(),
            new TestTokenService(),
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_client", result.ErrorCode);
        Assert.Equal("client_id is required.", result.Message);
    }

    [Fact]
    public async Task TokenExchangeGrant_ForPublicClient_ShouldNotEmitActClaim()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .AddSingleton<IOAuthTokenExchangeHandler, DefaultOAuthTokenExchangeHandler>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.TokenExchange,
            ClientId = "public-app",
            SubjectToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "user-1"), new Claim(OAuthClaimTypes.Scope, "jobs.read")]),
            ActorToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "client:order-service"), new Claim(OAuthClaimTypes.Scope, "jobs.read")]),
            SubjectTokenType = OAuthTokenTypes.AccessToken,
            ActorTokenType = OAuthTokenTypes.AccessToken,
            RequestedTokenType = OAuthTokenTypes.AccessToken,
            Scope = "jobs.read"
        };
        var subjectService = new TestOAuthSubjectService();
        var tokenService = new TestTokenService();

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromTokenExchangeAsync(
            httpContext.Request,
            request,
            services,
            subjectService,
            tokenService,
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.DoesNotContain(tokenService.LastRequest!.AccessClaims!, claim => claim.Type == OAuthClaimTypes.Actor);
    }

    [Fact]
    public async Task TokenExchangeGrant_ShouldUseCustomHandler()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IOAuthClientManagementService>(new TestOAuthClientManagementService())
            .AddSingleton<IOAuthTokenExchangeHandler, CustomOAuthTokenExchangeHandler>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        using var rsa = RSA.Create(2048);
        var signingKey = new ManagedSigningKey
        {
            Kid = "kid-1",
            Key = new RsaSecurityKey(rsa) { KeyId = "kid-1" },
            CreatedAtUtc = DateTime.UtcNow,
            ActivatedAtUtc = DateTime.UtcNow
        };
        var request = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.TokenExchange,
            ClientId = "service-a",
            ClientSecret = "secret-a",
            SubjectToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "user-1"), new Claim(OAuthClaimTypes.Scope, "jobs.read jobs.write")]),
            ActorToken = CreateAccessToken(
                signingKey.Key,
                "https://issuer.local/oauth2",
                "jobs-api",
                [new Claim(OAuthClaimTypes.Subject, "client:order-service"), new Claim(OAuthClaimTypes.Actor, """{"sub":"client:web-app"}"""), new Claim(OAuthClaimTypes.Scope, "jobs.read jobs.write")]),
            SubjectTokenType = OAuthTokenTypes.AccessToken,
            ActorTokenType = OAuthTokenTypes.AccessToken,
            RequestedTokenType = OAuthTokenTypes.AccessToken,
            Scope = "jobs.read jobs.write"
        };
        var tokenService = new TestTokenService();

        var result = await Microsoft.AspNetCore.Routing.NOFOidcServerExtensions.TokenFromTokenExchangeAsync(
            httpContext.Request,
            request,
            services,
            new TestOAuthSubjectService(),
            tokenService,
            new StaticSigningKeyService(signingKey),
            new OAuthAuthorizationServerOptions
            {
                Issuer = "https://issuer.local/oauth2",
                AccessTokenAudience = "jobs-api",
                AccessTokenExpiration = TimeSpan.FromMinutes(20)
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("jobs.write", result.Value.Scope);
        Assert.Contains(tokenService.LastRequest!.AccessClaims!, claim => claim.Type == "custom.exchange" && claim.Value == "enabled");
        Assert.DoesNotContain(tokenService.LastRequest.AccessClaims!, claim => claim.Type == OAuthClaimTypes.Actor);
    }

    [Fact]
    public async Task AddOidcServer_ShouldPersistSigningKeysAcrossHostRestarts()
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
    public async Task AddOidcServer_WithoutEncryptionKey_ShouldGenerateLocalFallbackSecret()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(options =>
        {
            options.Issuer = "https://issuer.local";
        });
        builder.UseDbContext<NOFDbContext>()
            .WithConnectionString($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();

        var signingKey = await scope.GetRequiredService<ISigningKeyService>().GetCurrentSigningKeyAsync();
        var options = scope.GetRequiredService<IOptions<OAuthAuthorizationServerOptions>>().Value;

        Assert.False(string.IsNullOrWhiteSpace(signingKey.Kid));
        Assert.False(string.IsNullOrWhiteSpace(options.SigningKeyEncryptionKey));
    }

    [Fact]
    public async Task AddAuthenticationResourceServer_ShouldRegisterSeparately()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServerIssuer = "https://auth.local";
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
        Assert.Equal("https://auth.local", resourceOptions.AuthorizationServerIssuer);
        Assert.Equal(2, resourceOptions.Sources.Count);
        Assert.Contains(resourceOptions.Sources, source =>
            source.HeaderName == "Authorization" &&
            source.TokenType == "Bearer");
        Assert.Contains(resourceOptions.Sources, source =>
            source.HeaderName == "X-Authorization" &&
            source.TokenType == "Token");
        var jwksService1 = scope.GetRequiredService<IJwksService>();
        var jwksService2 = scope.GetRequiredService<IJwksService>();
        Assert.IsType<HttpAuthorizationServerService>(jwksService1);
        Assert.IsType<HttpAuthorizationServerService>(jwksService2);
        Assert.Same(jwksService1, jwksService2);
        Assert.NotNull(scope.GetRequiredService<ResourceServerJwksCacheService>());
        Assert.IsType<DefaultInboundAuthorizationHandler>(scope.GetRequiredService<IInboundAuthorizationHandler>());
    }

    [Fact]
    public async Task AddOidcServer_AndResourceServer_ShouldAllowSingletonJwksCacheToRefreshViaScope()
    {
        var builder = CreateAuthorityBuilder($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared");
        builder.Services.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServerIssuer = "https://issuer.local";
            options.ExpectedIssuer = "https://issuer.local";
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
        builder.Services.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServerIssuer = "https://auth.local";
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
            descriptor.ServiceType == typeof(IRequestOutboundMiddleware) &&
            descriptor.ImplementationType?.Name.Contains("JwtTokenPropagationOutboundMiddleware", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(ICommandOutboundMiddleware) &&
            descriptor.ImplementationType?.Name.Contains("JwtTokenPropagationOutboundMiddleware", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(INotificationOutboundMiddleware) &&
            descriptor.ImplementationType?.Name.Contains("JwtTokenPropagationOutboundMiddleware", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task AddAuthenticationResourceServer_Only_ShouldNotRegisterAuthorityServicesExplicitly()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.Services.AddAuthenticationResourceServer(options =>
        {
            options.AuthorizationServerIssuer = "https://auth.local";
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
        builder.AddOidcServer(options =>
        {
            options.Issuer = "https://issuer.local";
            options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
        });
        builder.UseDbContext<NOFDbContext>()
            .WithConnectionString(connectionString)
            .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

        return builder;
    }

    private sealed class TestOAuthClientManagementService : IOAuthClientManagementService
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
                        [new(OAuthClaimTypes.ClientId, request.ClientId)]));
            }

            return ValueTask.FromResult<OAuthClientCredentialsValidationResult>(
                new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client credentials are invalid."));
        }

        public Task<IReadOnlyList<OAuthClientDescriptor>> ListAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<OAuthClientDescriptor>> GetAsync(string clientId, CancellationToken cancellationToken = default)
        {
            if (string.Equals(clientId, "service-a", StringComparison.Ordinal))
            {
                return Task.FromResult(Result.Success(new OAuthClientDescriptor
                {
                    ClientId = "service-a",
                    DisplayName = "Service A",
                    AllowedScopes = ["jobs.read", "jobs.write"],
                    RedirectUris = ["https://service.local/callback"],
                    AccessTokenClaims = [],
                    ClientType = OAuthClientType.Confidential,
                    IsEnabled = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }));
            }

            if (string.Equals(clientId, "public-app", StringComparison.Ordinal))
            {
                return Task.FromResult(Result.Success(new OAuthClientDescriptor
                {
                    ClientId = "public-app",
                    DisplayName = "Public App",
                    AllowedScopes = ["jobs.read"],
                    RedirectUris = ["https://app.local/callback"],
                    AccessTokenClaims = [],
                    ClientType = OAuthClientType.Public,
                    IsEnabled = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }));
            }

            if (string.Equals(clientId, "multi-redirect-app", StringComparison.Ordinal))
            {
                return Task.FromResult(Result.Success(new OAuthClientDescriptor
                {
                    ClientId = "multi-redirect-app",
                    DisplayName = "Multi Redirect App",
                    AllowedScopes = ["jobs.read"],
                    RedirectUris = ["https://app.local/callback", "https://app.local/signout"],
                    AccessTokenClaims = [],
                    ClientType = OAuthClientType.Public,
                    IsEnabled = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }));
            }

            return Task.FromResult<Result<OAuthClientDescriptor>>(Result.Fail("not_found", "OAuth client was not found."));
        }

        public Task<Result<OAuthClientSecretDescriptor>> CreateAsync(CreateOAuthClientRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<OAuthClientDescriptor>> UpdateAsync(string clientId, UpdateOAuthClientRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<OAuthClientSecretDescriptor>> RotateSecretAsync(string clientId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> DeleteAsync(string clientId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestTokenService : ITokenService
    {
        public IssueTokenRequest? LastRequest { get; private set; }
        public RevokeRefreshTokenRequest? LastRevokeRequest { get; private set; }
        public Result<ValidateRefreshTokenResponse> ValidateRefreshTokenResult { get; set; }
            = Result.Fail("401", "Refresh token is invalid.");
        public Result RevokeRefreshTokenResult { get; set; } = Result.Success();
        public Result<IntrospectTokenResponse> IntrospectTokenResult { get; set; }
            = Result.Success(new IntrospectTokenResponse { Active = false });

        public Task<Result<IssueTokenResponse>> IssueTokenAsync(IssueTokenRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Result.Success(new IssueTokenResponse
            {
                AccessToken = "access-token",
                AccessTokenExpiresAtUtc = DateTime.UtcNow.Add(request.AccessTokenExpiration),
                RefreshToken = request.RefreshToken is null
                    ? null
                    : new IssuedRefreshToken
                    {
                        Token = "refresh-token-issued",
                        ExpiresAtUtc = DateTime.UtcNow.Add(request.RefreshToken.Expiration)
                    }
            }));
        }

        public Task<Result<ValidateRefreshTokenResponse>> ValidateRefreshTokenAsync(ValidateRefreshTokenRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ValidateRefreshTokenResult);

        public Task<Result> RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken)
        {
            LastRevokeRequest = request;
            return Task.FromResult(RevokeRefreshTokenResult);
        }

        public Task<Result<IntrospectTokenResponse>> IntrospectTokenAsync(IntrospectTokenRequest request, CancellationToken cancellationToken)
            => Task.FromResult(IntrospectTokenResult);
    }

    private sealed class CustomOAuthTokenExchangeHandler : IOAuthTokenExchangeHandler
    {
        public ValueTask<OAuthTokenExchangeResult> HandleAsync(
            OAuthTokenExchangeRequest request,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            return ValueTask.FromResult<OAuthTokenExchangeResult>(
                new OAuthTokenExchangeResult.Success(
                    request.Subject,
                    new HashSet<string>(["jobs.write"], StringComparer.Ordinal),
                    [new TokenClaim("custom.exchange", "enabled")]));
        }
    }

    private sealed class TestOAuthSubjectService : IOAuthSubjectService
    {
        public IReadOnlySet<string> LastRequestedScopes { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

        public ValueTask<OAuthSubjectProfile?> GetProfileAsync(string subject, IReadOnlySet<string> scopes, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            LastRequestedScopes = scopes;
            return ValueTask.FromResult<OAuthSubjectProfile?>(OAuthSubjectProfile.Create(subject));
        }
    }

    private sealed class StaticSigningKeyService(ManagedSigningKey signingKey) : ISigningKeyService
    {
        public Task<ManagedSigningKey> GetCurrentSigningKeyAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(signingKey);
        }

        public Task<ManagedSigningKey[]> GetAllKeysAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<ManagedSigningKey[]>([signingKey]);
        }

        public Task RotateKeyAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            throw new NotSupportedException();
        }
    }

    private static string CreateAccessToken(SecurityKey key, string issuer, string audience, IReadOnlyList<Claim> claims)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
