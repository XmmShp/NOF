using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;

namespace NOF.Infrastructure;

/// <summary>
/// Caches resource-server validation keys and refreshes them on demand.
/// </summary>
public sealed class ResourceServerJwksCacheService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _minimumRefreshInterval;
    private readonly string? _configuredIssuer;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _missingKidRefreshAttempts = new(StringComparer.Ordinal);

    private IReadOnlyList<SecurityKey> _cachedKeys = [];
    private OAuthAuthorizationServerMetadataDocument? _cachedMetadata;
    private DateTimeOffset? _lastSuccessfulRefreshAtUtc;

    public ResourceServerJwksCacheService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuthenticationResourceServerOptions> options)
        : this(scopeFactory, options, TimeProvider.System)
    {
    }

    public ResourceServerJwksCacheService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuthenticationResourceServerOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _configuredIssuer = string.IsNullOrWhiteSpace(options.Value.ExpectedIssuer)
            ? null
            : options.Value.ExpectedIssuer;
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

    public async Task<IReadOnlyList<SecurityKey>> GetSecurityKeysAsync(
        string? requiredKid,
        CancellationToken cancellationToken = default)
    {
        var snapshot = CreateSnapshot();
        if (CanUseSnapshot(snapshot, requiredKid))
        {
            return snapshot.Keys;
        }

        return await RefreshRequiredAsync(requiredKid, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetIssuerAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_configuredIssuer))
        {
            return _configuredIssuer;
        }

        var snapshot = CreateSnapshot();
        if (snapshot.Metadata?.Issuer is { Length: > 0 } issuer && snapshot.Age < _minimumRefreshInterval)
        {
            return issuer;
        }

        await RefreshRequiredAsync(cancellationToken).ConfigureAwait(false);
        return _cachedMetadata?.Issuer;
    }

    public void Invalidate()
    {
        _cachedKeys = [];
        _cachedMetadata = null;
        _lastSuccessfulRefreshAtUtc = null;
        _missingKidRefreshAttempts.Clear();
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

    private async Task<IReadOnlyList<SecurityKey>> RefreshRequiredAsync(
        string? requiredKid,
        CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = CreateSnapshot();
            if (CanUseSnapshot(snapshot, requiredKid))
            {
                return snapshot.Keys;
            }

            MarkMissingKidRefreshAttempt(snapshot, requiredKid);

            var keys = await RefreshUnsafeAsync(cancellationToken).ConfigureAwait(false);
            ClearSatisfiedMissingKidRefreshAttempt(requiredKid, keys);
            return keys;
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
            var metadata = await GetMetadataAsync(cancellationToken).ConfigureAwait(false);
            var document = await GetJwksAsync(cancellationToken).ConfigureAwait(false);
            var keys = JwksSecurityKeyConverter.ToSecurityKeys(document.Keys);

            if (keys.Length == 0 && previousSnapshot.HasUsableKeys)
            {
                return previousSnapshot.Keys;
            }

            _cachedKeys = keys;
            _cachedMetadata = metadata;
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
        scope.ServiceProvider.ResolveDaemonServices();
        var jwksService = scope.ServiceProvider.GetRequiredService<IJwksService>();
        return await jwksService.GetJwksAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<OAuthAuthorizationServerMetadataDocument?> GetMetadataAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var metadataService = scope.ServiceProvider.GetService<IAuthorizationServerMetadataService>();
        return metadataService is null
            ? null
            : await metadataService.GetMetadataAsync(cancellationToken).ConfigureAwait(false);
    }

    private CacheSnapshot CreateSnapshot()
    {
        var lastSuccessfulRefreshAtUtc = _lastSuccessfulRefreshAtUtc;
        var age = lastSuccessfulRefreshAtUtc is null
            ? TimeSpan.MaxValue
            : _timeProvider.GetUtcNow() - lastSuccessfulRefreshAtUtc.Value;

        return new CacheSnapshot(_cachedKeys, _cachedMetadata, age);
    }

    private bool CanUseSnapshot(CacheSnapshot snapshot, string? requiredKid)
    {
        if (!snapshot.HasUsableKeys)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requiredKid))
        {
            return snapshot.Age < _minimumRefreshInterval;
        }

        if (snapshot.Contains(requiredKid))
        {
            _missingKidRefreshAttempts.TryRemove(requiredKid, out _);
            return true;
        }

        if (snapshot.Age >= _minimumRefreshInterval)
        {
            return false;
        }

        // A missing kid can indicate key rotation happened before the soft TTL elapsed.
        // We still only want to probe the authorization server once per kid within the
        // current refresh window, otherwise a burst of invalid tokens would turn into a
        // thundering herd of metadata/JWKS requests.
        return HasRecentMissingKidRefreshAttempt(requiredKid);
    }

    private bool HasRecentMissingKidRefreshAttempt(string requiredKid)
    {
        if (!_missingKidRefreshAttempts.TryGetValue(requiredKid, out var attemptedAtUtc))
        {
            return false;
        }

        return _timeProvider.GetUtcNow() - attemptedAtUtc < _minimumRefreshInterval;
    }

    private void MarkMissingKidRefreshAttempt(CacheSnapshot snapshot, string? requiredKid)
    {
        if (!snapshot.HasUsableKeys
            || string.IsNullOrWhiteSpace(requiredKid)
            || snapshot.Contains(requiredKid)
            || snapshot.Age >= _minimumRefreshInterval)
        {
            return;
        }

        _missingKidRefreshAttempts[requiredKid] = _timeProvider.GetUtcNow();
    }

    private void ClearSatisfiedMissingKidRefreshAttempt(string? requiredKid, IReadOnlyList<SecurityKey> keys)
    {
        if (string.IsNullOrWhiteSpace(requiredKid))
        {
            return;
        }

        if (keys.Any(key => string.Equals(key.KeyId, requiredKid, StringComparison.Ordinal)))
        {
            _missingKidRefreshAttempts.TryRemove(requiredKid, out _);
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }

    private readonly record struct CacheSnapshot(
        IReadOnlyList<SecurityKey> Keys,
        OAuthAuthorizationServerMetadataDocument? Metadata,
        TimeSpan Age)
    {
        public bool HasUsableKeys => Keys.Count > 0;

        public bool Contains(string requiredKid)
            => Keys.Any(key => string.Equals(key.KeyId, requiredKid, StringComparison.Ordinal));
    }
}
