using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

/// <summary>
/// Background service that periodically rotates the JWT signing key
/// and refreshes cached JWKS registrations.
/// </summary>
public sealed class SigningKeyRotationBackgroundService : BackgroundService
{
    private static readonly TimeSpan KeyRotationLockExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan KeyRotationLockTimeout = TimeSpan.FromSeconds(5);
    private const string KeyRotationLockKeyPrefix = "jwt-signing-key-rotation";

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuthenticationAuthorityOptions _options;
    private readonly ILogger<SigningKeyRotationBackgroundService> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public SigningKeyRotationBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AuthenticationAuthorityOptions> options,
        ILogger<SigningKeyRotationBackgroundService> logger,
        IHostEnvironment hostEnvironment)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Key rotation background service started. Interval: {Interval}",
            _options.KeyRotationInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_hostEnvironment.IsPrimaryNodeEnvironment)
                {
                    _logger.LogDebug(
                        "Skipping JWT key rotation on non-primary node {InstanceId}",
                        _hostEnvironment.InstanceId);
                    await Task.Delay(_options.KeyRotationInterval, stoppingToken);
                    continue;
                }

                var nextRotation = await ComputeNextRotationDelayAsync(stoppingToken).ConfigureAwait(false);

                _logger.LogDebug("Next key rotation in {Delay}", nextRotation);

                await Task.Delay(nextRotation, stoppingToken);

                using var scope = _serviceScopeFactory.CreateScope();
                scope.ServiceProvider.ResolveDaemonServices();
                if (!_hostEnvironment.IsPrimaryNodeEnvironment)
                {
                    _logger.LogDebug(
                        "Skipping JWT key rotation on non-primary node {InstanceId}",
                        _hostEnvironment.InstanceId);
                    continue;
                }

                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                var rotationLock = await cacheService
                    .IgnoreQueryFilters()
                    .TryAcquireLockAsync(
                        $"{KeyRotationLockKeyPrefix}:{_options.Issuer}",
                        KeyRotationLockExpiration,
                        KeyRotationLockTimeout,
                        stoppingToken)
                    .ConfigureAwait(false);
                if (!rotationLock.HasValue)
                {
                    _logger.LogDebug("Skipping JWT key rotation because another node holds the rotation lock.");
                    continue;
                }

                await using var _ = rotationLock.Value;
                var signingKeyService = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
                var recomputedNextRotation = await ComputeNextRotationDelayAsync(stoppingToken).ConfigureAwait(false);
                if (recomputedNextRotation > TimeSpan.Zero)
                {
                    _logger.LogDebug(
                        "Skipping JWT key rotation because the current key was already rotated. Next key rotation in {Delay}",
                        recomputedNextRotation);
                    continue;
                }

                await signingKeyService.RotateKeyAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("Signing key rotated successfully. New kid: {Kid}",
                    (await signingKeyService.GetCurrentSigningKeyAsync(stoppingToken).ConfigureAwait(false)).Kid);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during key rotation");
            }
        }

        _logger.LogInformation("Key rotation background service stopped");
    }

    /// <summary>
    /// Computes the delay until the next rotation based on the current key's age
    /// and the configured rotation interval.
    /// </summary>
    private async Task<TimeSpan> ComputeNextRotationDelayAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var signingKeyService = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
        var keyAge = DateTime.UtcNow - (await signingKeyService.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false)).ActivatedAtUtc;
        var remaining = _options.KeyRotationInterval - keyAge;

        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
