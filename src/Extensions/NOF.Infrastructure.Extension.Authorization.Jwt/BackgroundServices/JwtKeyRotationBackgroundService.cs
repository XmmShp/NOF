using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Background service that periodically rotates the JWT signing key
/// and publishes a <see cref="JwtKeyRotationNotification"/> so that all instances
/// in a distributed deployment refresh their cached JWKS.
/// </summary>
public sealed class JwtKeyRotationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly JwtAuthorityOptions _options;
    private readonly ILogger<JwtKeyRotationBackgroundService> _logger;

    public JwtKeyRotationBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<JwtAuthorityOptions> options,
        ILogger<JwtKeyRotationBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
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
                var nextRotation = ComputeNextRotationDelay();

                _logger.LogDebug("Next key rotation in {Delay}", nextRotation);

                await Task.Delay(nextRotation, stoppingToken);

                using var scope = _serviceScopeFactory.CreateScope();
                var signingKeyService = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
                signingKeyService.RotateKey();
                _logger.LogInformation("Signing key rotated successfully. New kid: {Kid}",
                    signingKeyService.CurrentSigningKey.Kid);

                var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
                await notificationPublisher.PublishAsync(new JwtKeyRotationNotification(), cancellationToken: stoppingToken);
                _logger.LogInformation("Key rotation notification published");
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
    private TimeSpan ComputeNextRotationDelay()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var signingKeyService = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();
        var keyAge = DateTime.UtcNow - signingKeyService.CurrentSigningKey.CreatedAtUtc;
        var remaining = _options.KeyRotationInterval - keyAge;

        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
