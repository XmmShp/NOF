using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Test;
using System.Text.Json;
using Xunit;
using OidcRoutes = Microsoft.AspNetCore.Routing.NOFOidcServerExtensions;

namespace NOF.Infrastructure.Tests.Authentication.Extensions;

public sealed class OAuthDeviceFlowTests
{
    private const string SigningKeyEncryptionKey = "device-flow-signing-key-passphrase-for-tests";

    [Fact]
    public async Task AddDeviceClient_ShouldBootstrapDeviceOnlyPublicClient()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(ConfigureOptions)
            .AddDeviceClient(
                "living-room-tv",
                [OAuthScope.OpenId, OAuthScope.Profile, OAuthScope.OfflineAccess],
                displayName: "Living Room TV");

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var clientRepository = scope.GetRequiredService<IOAuthClientRepository>();

        var result = await clientRepository.GetAsync("living-room-tv");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(OAuthClientType.Public, result.Value.ClientType);
        Assert.Equal(OAuthClientAuthenticationMethods.None, result.Value.TokenEndpointAuthenticationMethod);
        Assert.Equal(
            [OAuthGrantTypes.RefreshToken, OAuthGrantTypes.DeviceCode],
            result.Value.AllowedGrantTypes.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
        Assert.Empty(result.Value.AllowedResponseTypes);
        Assert.Empty(result.Value.RedirectUris);
    }

    [Fact]
    public async Task DeviceFlow_ShouldPollApproveAndRedeemExactlyOnceWithRetryGrace()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var builder = CreateDeviceFlowBuilder(timeProvider);

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var deviceEndpoint = scope.GetRequiredService<IOAuthDeviceAuthorizationEndpoint>();
        var deviceGrantService = scope.GetRequiredService<IOAuthDeviceGrantService>();
        var authorizationContext = CreateHttpsContext(scope.Services);
        var authorizationResult = await deviceEndpoint.HandleAsync(
            new OAuthDeviceAuthorizationEndpointRequest(
                authorizationContext.Request,
                new OAuthDeviceAuthorizationRequest
                {
                    ClientId = "living-room-tv",
                    Scope = $"{OAuthScope.OpenId} {OAuthScope.Profile} {OAuthScope.OfflineAccess}"
                }),
            CancellationToken.None);
        var authorization = await ExecuteJsonAsync<OAuthDeviceAuthorizationResponse>(
            authorizationResult,
            authorizationContext);

        Assert.Equal("https://issuer.local/oauth2/device", authorization.VerificationUri);
        Assert.Contains($"user_code={authorization.UserCode}", authorization.VerificationUriComplete, StringComparison.Ordinal);
        Assert.Equal(5, authorization.Interval);
        Assert.Equal("no-store", authorizationContext.Response.Headers.CacheControl);

        var pendingDescriptor = await deviceGrantService.GetPendingAsync(
            authorization.UserCode.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal));
        Assert.True(pendingDescriptor.IsSuccess, pendingDescriptor.Message);
        Assert.Equal("Living Room TV", pendingDescriptor.Value.ClientDisplayName);

        var wrongClient = await deviceGrantService.RedeemAsync(authorization.DeviceCode, "different-client");
        Assert.False(wrongClient.IsSuccess);
        Assert.Equal("invalid_grant", wrongClient.ErrorCode);

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        var tokenContext = CreateHttpsContext(scope.Services);
        var tokenRequest = new OAuthTokenRequest
        {
            GrantType = OAuthGrantTypes.DeviceCode,
            DeviceCode = authorization.DeviceCode,
            ClientId = "living-room-tv"
        };
        var pending = await OidcRoutes.TokenFromDeviceCodeAsync(
            tokenContext.Request,
            tokenRequest,
            scope.Services,
            deviceGrantService,
            CancellationToken.None);
        var tooFast = await OidcRoutes.TokenFromDeviceCodeAsync(
            tokenContext.Request,
            tokenRequest,
            scope.Services,
            deviceGrantService,
            CancellationToken.None);

        Assert.False(pending.IsSuccess);
        Assert.Equal("authorization_pending", pending.ErrorCode);
        Assert.False(tooFast.IsSuccess);
        Assert.Equal("slow_down", tooFast.ErrorCode);

        var approval = await deviceGrantService.ApproveAsync(authorization.UserCode, "user-42");
        var responses = await Task.WhenAll(
            deviceGrantService.RedeemAsync(authorization.DeviceCode, "living-room-tv"),
            deviceGrantService.RedeemAsync(authorization.DeviceCode, "living-room-tv"));
        var response = responses[0];
        var retry = responses[1];

        Assert.True(approval.IsSuccess, approval.Message);
        Assert.True(response.IsSuccess, response.Message);
        Assert.NotNull(response.Value.IdToken);
        Assert.NotNull(response.Value.RefreshToken);
        Assert.True(retry.IsSuccess, retry.Message);
        Assert.Equal(response.Value.AccessToken, retry.Value.AccessToken);
        Assert.Equal(response.Value.RefreshToken, retry.Value.RefreshToken);

        var noLongerPending = await deviceGrantService.GetPendingAsync(authorization.UserCode);
        Assert.False(noLongerPending.IsSuccess);
        Assert.Equal("invalid_user_code", noLongerPending.ErrorCode);
    }

    [Fact]
    public async Task DeviceFlow_ShouldReturnAccessDeniedAfterUserDenial()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var builder = CreateDeviceFlowBuilder(timeProvider);

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var service = scope.GetRequiredService<IOAuthDeviceGrantService>();
        var created = await service.CreateAsync(new CreateOAuthDeviceGrantRequest
        {
            ClientId = "living-room-tv",
            ClientDisplayName = "Living Room TV",
            Scopes = new HashSet<string>([OAuthScope.OpenId], StringComparer.Ordinal)
        });
        var createdValue = Assert.IsType<OAuthDeviceAuthorizationResponse>(created.Value);

        var denied = await service.DenyAsync(createdValue.UserCode);
        var redeemed = await service.RedeemAsync(createdValue.DeviceCode, "living-room-tv");

        Assert.True(created.IsSuccess, created.Message);
        Assert.True(denied.IsSuccess, denied.Message);
        Assert.False(redeemed.IsSuccess);
        Assert.Equal("access_denied", redeemed.ErrorCode);
    }

    [Fact]
    public async Task DeviceFlow_ShouldReturnExpiredTokenAfterExpiration()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var builder = CreateDeviceFlowBuilder(timeProvider, options => options.DeviceCodeExpiration = TimeSpan.FromMinutes(1));

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var service = scope.GetRequiredService<IOAuthDeviceGrantService>();
        var created = await service.CreateAsync(new CreateOAuthDeviceGrantRequest
        {
            ClientId = "living-room-tv",
            ClientDisplayName = "Living Room TV",
            Scopes = new HashSet<string>([OAuthScope.OpenId], StringComparer.Ordinal)
        });
        var createdValue = Assert.IsType<OAuthDeviceAuthorizationResponse>(created.Value);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        var redeemed = await service.RedeemAsync(createdValue.DeviceCode, "living-room-tv");

        Assert.True(created.IsSuccess, created.Message);
        Assert.False(redeemed.IsSuccess);
        Assert.Equal("expired_token", redeemed.ErrorCode);
    }

    [Fact]
    public async Task Metadata_ShouldAdvertiseDeviceAuthorizationGrant()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(ConfigureOptions);

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var endpoint = scope.GetRequiredService<IOAuthMetadataEndpoint>();
        var context = new DefaultHttpContext { RequestServices = scope.Services };
        var result = await endpoint.HandleAsync(CancellationToken.None);
        var metadata = await ExecuteJsonAsync<OAuthServerMetadata>(result, context);

        Assert.Equal("https://issuer.local/oauth2/device_authorization", metadata.DeviceAuthorizationEndpoint);
        Assert.Contains(OAuthGrantTypes.DeviceCode, metadata.GrantTypesSupported);
    }

    [Fact]
    public async Task DeviceAuthorizationEndpoint_ShouldRequireHttpsByDefault()
    {
        var builder = CreateDeviceFlowBuilder(new MutableTimeProvider(DateTimeOffset.UtcNow));

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var endpoint = scope.GetRequiredService<IOAuthDeviceAuthorizationEndpoint>();
        var context = new DefaultHttpContext { RequestServices = scope.Services };
        var result = await endpoint.HandleAsync(
            new OAuthDeviceAuthorizationEndpointRequest(
                context.Request,
                new OAuthDeviceAuthorizationRequest
                {
                    ClientId = "living-room-tv",
                    Scope = OAuthScope.OpenId
                }),
            CancellationToken.None);
        var error = await ExecuteJsonAsync<OAuthError>(result, context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("invalid_request", error.Error);
    }

    [Fact]
    public async Task DynamicRegistration_ShouldAllowDeviceOnlyClientWithoutRedirectOrResponseType()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(ConfigureOptions);

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var endpoint = new DefaultOAuthClientRegistrationEndpoint(
            new AnonymousInitialAccessTokenHandler(),
            scope.GetRequiredService<IOAuthClientRegistrationRepository>(),
            scope.GetRequiredService<IOptions<OAuthAuthorizationServerOptions>>());
        var context = CreateHttpsContext(scope.Services);
        var result = await endpoint.RegisterAsync(
            context.Request,
            new OAuthClientRegistrationRequest
            {
                TokenEndpointAuthenticationMethod = OAuthClientAuthenticationMethods.None,
                GrantTypes = [OAuthGrantTypes.DeviceCode, OAuthGrantTypes.RefreshToken],
                ClientName = "Dynamically Registered TV",
                Scope = $"{OAuthScope.OpenId} {OAuthScope.OfflineAccess}",
                ApplicationType = OAuthClientApplicationTypes.Native
            },
            CancellationToken.None);
        var registration = await ExecuteJsonAsync<OAuthClientRegistrationResponse>(result, context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Empty(registration.RedirectUris);
        Assert.Empty(registration.ResponseTypes);
        Assert.Contains(OAuthGrantTypes.DeviceCode, registration.GrantTypes);
    }

    private static NOFTestAppBuilder CreateDeviceFlowBuilder(
        MutableTimeProvider timeProvider,
        Action<OAuthAuthorizationServerOptions>? configure = null)
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(options =>
            {
                ConfigureOptions(options);
                configure?.Invoke(options);
            })
            .AddDeviceClient(
                "living-room-tv",
                [OAuthScope.OpenId, OAuthScope.Profile, OAuthScope.OfflineAccess],
                displayName: "Living Room TV");
        builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(timeProvider));
        builder.Services.AddScoped<IOAuthSubjectService, TestOAuthSubjectService>();
        return builder;
    }

    private static void ConfigureOptions(OAuthAuthorizationServerOptions options)
    {
        options.Issuer = "https://issuer.local/oauth2";
        options.AccessTokenAudience = "device-flow-tests";
        options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
    }

    private static DefaultHttpContext CreateHttpsContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<T> ExecuteJsonAsync<T>(
        Microsoft.AspNetCore.Http.IResult result,
        DefaultHttpContext context)
    {
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<T>(
            context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class TestOAuthSubjectService : IOAuthSubjectService
    {
        public ValueTask<OAuthSubjectProfile?> GetProfileAsync(
            string subject,
            IReadOnlySet<string> scopes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<OAuthSubjectProfile?>(OAuthSubjectProfile.Create(
                subject,
                identityClaims: [new(OAuthClaimTypes.Name, "Device User")]));
        }
    }

    private sealed class AnonymousInitialAccessTokenHandler : IOAuthInitialAccessTokenHandler
    {
        public Task<Result> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success());
        }
    }
}
