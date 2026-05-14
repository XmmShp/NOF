using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Background service that periodically rotates the JWT signing key
/// and refreshes cached JWKS registrations.
/// </summary>
public sealed class JwtKeyRotationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly JwtAuthorityOptions _options;
    private readonly ILogger<JwtKeyRotationBackgroundService> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public JwtKeyRotationBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<JwtAuthorityOptions> options,
        ILogger<JwtKeyRotationBackgroundService> logger,
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
                var signingKeyService = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
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
        var signingKeyService = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
        var keyAge = DateTime.UtcNow - (await signingKeyService.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false)).ActivatedAtUtc;
        var remaining = _options.KeyRotationInterval - keyAge;

        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
