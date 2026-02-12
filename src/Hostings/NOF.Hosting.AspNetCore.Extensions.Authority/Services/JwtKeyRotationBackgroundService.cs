using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Background service that periodically rotates the JWT signing key
/// and publishes a <see cref="JwtKeyRotationNotification"/> so that all instances
/// in a distributed deployment refresh their cached JWKS.
/// </summary>
public sealed class JwtKeyRotationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthorityOptions _options;
    private readonly ILogger<JwtKeyRotationBackgroundService> _logger;

    public JwtKeyRotationBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<AuthorityOptions> options,
        ILogger<JwtKeyRotationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
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
                using var scope = _serviceProvider.CreateScope();
                var signingKeyService = scope.ServiceProvider.GetRequiredService<ISigningKeyService>();

                var nextRotation = ComputeNextRotationDelay(signingKeyService);

                _logger.LogDebug("Next key rotation in {Delay}", nextRotation);

                await Task.Delay(nextRotation, stoppingToken);

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
    private TimeSpan ComputeNextRotationDelay(ISigningKeyService signingKeyService)
    {
        var keyAge = DateTime.UtcNow - signingKeyService.CurrentSigningKey.CreatedAtUtc;
        var remaining = _options.KeyRotationInterval - keyAge;

        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
