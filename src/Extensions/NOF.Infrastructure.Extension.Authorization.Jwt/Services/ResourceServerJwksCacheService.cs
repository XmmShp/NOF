using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Caches resource-server validation keys and refreshes them on demand.
/// </summary>
public sealed class ResourceServerJwksCacheService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _minimumRefreshInterval;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<SecurityKey> _cachedKeys = [];
    private DateTimeOffset? _lastSuccessfulRefreshAtUtc;

    public ResourceServerJwksCacheService(
        IServiceScopeFactory scopeFactory,
        IOptions<JwtResourceServerOptions> options)
        : this(scopeFactory, options, TimeProvider.System)
    {
    }

    public ResourceServerJwksCacheService(
        IServiceScopeFactory scopeFactory,
        IOptions<JwtResourceServerOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _minimumRefreshInterval = options.Value.JwksRefreshInterval > TimeSpan.Zero
            ? options.Value.JwksRefreshInterval
            : TimeSpan.FromMinutes(10);
    }

    public async Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = CreateSnapshot();
        if (snapshot.HasUsableKeys && snapshot.Age < _minimumRefreshInterval)
        {
            return snapshot.Keys;
        }

        return await RefreshRequiredAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Invalidate()
    {
        _cachedKeys = [];
        _lastSuccessfulRefreshAtUtc = null;
    }

    public async Task<IReadOnlyList<SecurityKey>> RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Invalidate();
            return await RefreshUnsafeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<SecurityKey>> RefreshRequiredAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = CreateSnapshot();
            if (snapshot.HasUsableKeys && snapshot.Age < _minimumRefreshInterval)
            {
                return snapshot.Keys;
            }

            return await RefreshUnsafeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<SecurityKey>> RefreshUnsafeAsync(CancellationToken cancellationToken)
    {
        var previousSnapshot = CreateSnapshot();

        try
        {
            var document = await GetJwksAsync(cancellationToken).ConfigureAwait(false);
            var keys = JwksSecurityKeyConverter.ToSecurityKeys(document.Keys);

            if (keys.Length == 0 && previousSnapshot.HasUsableKeys)
            {
                return previousSnapshot.Keys;
            }

            _cachedKeys = keys;
            _lastSuccessfulRefreshAtUtc = _timeProvider.GetUtcNow();
            return _cachedKeys;
        }
        catch when (previousSnapshot.HasUsableKeys)
        {
            return previousSnapshot.Keys;
        }
    }

    private async Task<JwksDocument> GetJwksAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jwksService = scope.ServiceProvider.GetRequiredService<IJwksService>();
        return await jwksService.GetJwksAsync(cancellationToken).ConfigureAwait(false);
    }

    private CacheSnapshot CreateSnapshot()
    {
        var lastSuccessfulRefreshAtUtc = _lastSuccessfulRefreshAtUtc;
        var age = lastSuccessfulRefreshAtUtc is null
            ? TimeSpan.MaxValue
            : _timeProvider.GetUtcNow() - lastSuccessfulRefreshAtUtc.Value;

        return new CacheSnapshot(_cachedKeys, age);
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }

    private readonly record struct CacheSnapshot(
        IReadOnlyList<SecurityKey> Keys,
        TimeSpan Age)
    {
        public bool HasUsableKeys => Keys.Count > 0;
    }
}
