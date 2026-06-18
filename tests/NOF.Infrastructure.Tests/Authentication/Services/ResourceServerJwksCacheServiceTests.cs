using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.Services;

public sealed class ResourceServerJwksCacheServiceTests
{
    [Fact]
    public async Task GetSecurityKeysAsync_WithinSoftTtl_ShouldReuseCachedKeys()
    {
        var jwksService = new FakeJwksService([_ => Task.FromResult(CreateJwksDocument("kid-1"))]);
        var service = CreateService(jwksService);

        var first = await service.GetSecurityKeysAsync();
        var second = await service.GetSecurityKeysAsync();

        Assert.Equal("kid-1", Assert.Single(first).KeyId);
        Assert.Equal("kid-1", Assert.Single(second).KeyId);
        Assert.Equal(1, jwksService.RefreshCallCount);
    }

    [Fact]
    public async Task GetSecurityKeysAsync_AfterRefreshInterval_ShouldRefreshAndUseSingleflight()
    {
        var firstRefreshCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var jwksService = new FakeJwksService(
        [
            _ =>
            {
                firstRefreshCompleted.TrySetResult();
                return Task.FromResult(CreateJwksDocument("kid-1"));
            },
            async _ =>
            {
                secondRefreshStarted.TrySetResult();
                await releaseRefresh.Task;
                return CreateJwksDocument("kid-2");
            }
        ]);
        var service = CreateService(jwksService, timeProvider, TimeSpan.FromMinutes(10));

        _ = await service.GetSecurityKeysAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(11));

        await firstRefreshCompleted.Task;
        var refreshTask1 = service.GetSecurityKeysAsync();
        var refreshTask2 = service.GetSecurityKeysAsync();
        await secondRefreshStarted.Task;
        Assert.Equal(2, jwksService.RefreshCallCount);

        releaseRefresh.TrySetResult();
        var refreshed1 = await refreshTask1;
        var refreshed2 = await refreshTask2;

        Assert.Equal("kid-2", Assert.Single(refreshed1).KeyId);
        Assert.Equal("kid-2", Assert.Single(refreshed2).KeyId);
        Assert.Equal(2, jwksService.RefreshCallCount);
    }

    [Fact]
    public async Task GetSecurityKeysAsync_WhenRefreshFails_ShouldKeepPreviousKeys()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var jwksService = new FakeJwksService(
        [
            _ => Task.FromResult(CreateJwksDocument("kid-1")),
            _ => throw new InvalidOperationException("boom")
        ]);
        var service = CreateService(jwksService, timeProvider, TimeSpan.FromMinutes(10));

        _ = await service.GetSecurityKeysAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(11));

        var refreshed = await service.GetSecurityKeysAsync();

        Assert.Equal("kid-1", Assert.Single(refreshed).KeyId);
        Assert.Equal(2, jwksService.RefreshCallCount);
    }

    [Fact]
    public async Task RefreshNowAsync_ShouldBypassRefreshIntervalAndReloadKeys()
    {
        var jwksService = new FakeJwksService(
        [
            _ => Task.FromResult(CreateJwksDocument("kid-1")),
            _ => Task.FromResult(CreateJwksDocument("kid-2"))
        ]);
        var service = CreateService(jwksService, minimumRefreshInterval: TimeSpan.FromHours(1));

        var initial = await service.GetSecurityKeysAsync();
        var refreshed = await service.RefreshNowAsync();

        Assert.Equal("kid-1", Assert.Single(initial).KeyId);
        Assert.Equal("kid-2", Assert.Single(refreshed).KeyId);
        Assert.Equal(2, jwksService.RefreshCallCount);
    }

    [Fact]
    public async Task GetSecurityKeysAsync_WhenKidMissingBeforeMinimumRefreshInterval_ShouldReuseCachedKeys()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var jwksService = new FakeJwksService(
        [
            _ => Task.FromResult(CreateJwksDocument("kid-1")),
            _ => Task.FromResult(CreateJwksDocument("kid-2"))
        ]);
        var service = CreateService(jwksService, timeProvider, TimeSpan.FromMinutes(10));

        var initial = await service.GetSecurityKeysAsync();
        var reused = await service.GetSecurityKeysAsync("kid-2");

        Assert.Equal("kid-1", Assert.Single(initial).KeyId);
        Assert.Equal("kid-1", Assert.Single(reused).KeyId);
        Assert.Equal(1, jwksService.RefreshCallCount);
    }

    [Fact]
    public async Task GetSecurityKeysAsync_WhenKidMissingAfterMinimumRefreshInterval_ShouldRefreshKeys()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var jwksService = new FakeJwksService(
        [
            _ => Task.FromResult(CreateJwksDocument("kid-1")),
            _ => Task.FromResult(CreateJwksDocument("kid-2"))
        ]);
        var service = CreateService(jwksService, timeProvider, TimeSpan.FromMinutes(10));

        var initial = await service.GetSecurityKeysAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(11));
        var refreshed = await service.GetSecurityKeysAsync("kid-2");

        Assert.Equal("kid-1", Assert.Single(initial).KeyId);
        Assert.Equal("kid-2", Assert.Single(refreshed).KeyId);
        Assert.Equal(2, jwksService.RefreshCallCount);
    }

    [Fact]
    public async Task GetSecurityKeysAsync_WhenKidExistsAfterMinimumRefreshInterval_ShouldReuseCachedKeys()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var jwksService = new FakeJwksService(
        [
            _ => Task.FromResult(CreateJwksDocument("kid-1")),
            _ => Task.FromResult(CreateJwksDocument("kid-2"))
        ]);
        var service = CreateService(jwksService, timeProvider, TimeSpan.FromMinutes(10));

        var initial = await service.GetSecurityKeysAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(11));
        var reused = await service.GetSecurityKeysAsync("kid-1");

        Assert.Equal("kid-1", Assert.Single(initial).KeyId);
        Assert.Equal("kid-1", Assert.Single(reused).KeyId);
        Assert.Equal(1, jwksService.RefreshCallCount);
    }

    [Fact]
    public async Task GetIssuerAsync_WhenIssuerNotConfigured_ShouldUseDiscoveredMetadataIssuer()
    {
        var jwksService = new FakeJwksService(
            [_ => Task.FromResult(CreateJwksDocument("kid-1"))],
            [_ => Task.FromResult<OAuthAuthorizationServerMetadataDocument?>(new OAuthAuthorizationServerMetadataDocument
            {
                Issuer = "https://issuer.local/oauth2",
                JwksUri = "https://issuer.local/oauth2/.well-known/jwks.json"
            })]);
        var service = CreateService(jwksService);

        var issuer = await service.GetIssuerAsync();

        Assert.Equal("https://issuer.local/oauth2", issuer);
        Assert.Equal(1, jwksService.MetadataCallCount);
        Assert.Equal(1, jwksService.RefreshCallCount);
    }

    private static ResourceServerJwksCacheService CreateService(FakeJwksService jwksService, TimeProvider? timeProvider = null, TimeSpan? minimumRefreshInterval = null)
        => new(
            CreateScopeFactory(jwksService),
            Options.Create(new AuthenticationResourceServerOptions
            {
                JwksRefreshInterval = minimumRefreshInterval ?? TimeSpan.FromMinutes(10)
            }),
            timeProvider ?? TimeProvider.System);

    private static IServiceScopeFactory CreateScopeFactory(IJwksService jwksService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => jwksService);
        if (jwksService is IAuthorizationServerMetadataService metadataService)
        {
            services.AddScoped(_ => metadataService);
        }

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static JwksDocument CreateJwksDocument(string kid)
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(false);
        var modulus = Assert.IsType<byte[]>(parameters.Modulus);
        var exponent = Assert.IsType<byte[]>(parameters.Exponent);
        return new JwksDocument
        {
            Keys =
            [
                new JwkKeyDocument
                {
                    Kid = kid,
                    Kty = "RSA",
                    Use = "sig",
                    N = Base64UrlEncoder.Encode(modulus),
                    E = Base64UrlEncoder.Encode(exponent)
                }
            ]
        };
    }

    private sealed class FakeJwksService(
        IEnumerable<Func<CancellationToken, Task<JwksDocument>>> factories,
        IEnumerable<Func<CancellationToken, Task<OAuthAuthorizationServerMetadataDocument?>>>? metadataFactories = null) :
        IJwksService,
        IAuthorizationServerMetadataService
    {
        private readonly Queue<Func<CancellationToken, Task<JwksDocument>>> _factories = new(factories);
        private readonly Queue<Func<CancellationToken, Task<OAuthAuthorizationServerMetadataDocument?>>> _metadataFactories = new(metadataFactories ?? []);
        private Func<CancellationToken, Task<JwksDocument>>? _lastFactory;

        public int RefreshCallCount { get; private set; }

        public int MetadataCallCount { get; private set; }

        public async Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken = default)
        {
            RefreshCallCount++;
            _lastFactory = _factories.Count > 1 ? _factories.Dequeue() : _factories.Peek();
            return await _lastFactory(cancellationToken).ConfigureAwait(false);
        }

        public Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            MetadataCallCount++;
            if (_metadataFactories.Count == 0)
            {
                return Task.FromResult<OAuthAuthorizationServerMetadataDocument?>(null);
            }

            var factory = _metadataFactories.Count > 1 ? _metadataFactories.Dequeue() : _metadataFactories.Peek();
            return factory(cancellationToken);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
