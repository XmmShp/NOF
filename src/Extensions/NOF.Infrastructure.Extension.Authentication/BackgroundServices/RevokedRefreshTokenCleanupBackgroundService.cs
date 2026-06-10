using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.Extension.Authentication;

public sealed class RevokedRefreshTokenCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuthenticationAuthorityOptions _options;
    private readonly ILogger<RevokedRefreshTokenCleanupBackgroundService> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public RevokedRefreshTokenCleanupBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AuthenticationAuthorityOptions> options,
        ILogger<RevokedRefreshTokenCleanupBackgroundService> logger,
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
            "Revoked refresh token cleanup service started. Cleanup interval: {Interval}",
            _options.RevokedRefreshTokenCleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.RevokedRefreshTokenCleanupInterval, stoppingToken);
                if (!_hostEnvironment.IsPrimaryNodeEnvironment)
                {
                    _logger.LogDebug(
                        "Skipping revoked refresh token cleanup on non-primary node {InstanceId}",
                        _hostEnvironment.InstanceId);
                    continue;
                }

                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during revoked refresh token cleanup");
            }
        }

        _logger.LogInformation("Revoked refresh token cleanup service stopped");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NOFDbContext>();
        var now = DateTime.UtcNow;
        var deletedCount = await dbContext.Set<RevokedRefreshToken>()
            .Where(token => token.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Revoked refresh token cleanup completed. Deleted {Count} tokens expired before {Date}",
                deletedCount, now);
        }
        else
        {
            _logger.LogDebug("Revoked refresh token cleanup completed. No tokens to delete");
        }
    }
}
