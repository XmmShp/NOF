using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class PersistedSigningKeyCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly JwtAuthorityOptions _options;
    private readonly ILogger<PersistedSigningKeyCleanupBackgroundService> _logger;

    public PersistedSigningKeyCleanupBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<JwtAuthorityOptions> options,
        ILogger<PersistedSigningKeyCleanupBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Persisted signing key cleanup service started. Cleanup interval: {Interval}",
            _options.SigningKeyCleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.SigningKeyCleanupInterval, stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during persisted signing key cleanup");
            }
        }

        _logger.LogInformation("Persisted signing key cleanup service stopped");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NOFDbContext>();
        var cutoff = DateTime.UtcNow - _options.RevokedSigningKeyRetention;

        var deletedCount = await dbContext.Set<PersistedSigningKey>()
            .Where(key => key.Status == PersistedSigningKeyStatus.Revoked && key.InvalidatedAtUtc <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Persisted signing key cleanup completed. Deleted {Count} revoked keys invalidated before {Cutoff}",
                deletedCount,
                cutoff);
        }
        else
        {
            _logger.LogDebug("Persisted signing key cleanup completed. No keys to delete");
        }
    }
}
