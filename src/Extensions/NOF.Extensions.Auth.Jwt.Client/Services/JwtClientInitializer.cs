using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF;

namespace NOF;

/// <summary>
/// Service for initializing JWT client and retrieving JWT key on startup.
/// </summary>
public class JwtClientInitializer : IHostedService
{
    private readonly IRequestSender _requestSender;
    private readonly JwtValidationService _validationService;
    private readonly ILogger<JwtClientInitializer> _logger;

    public JwtClientInitializer(
        IRequestSender requestSender,
        JwtValidationService validationService,
        ILogger<JwtClientInitializer> logger)
    {
        _requestSender = requestSender;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing JWT client and retrieving JWKS...");

            // Pre-fetch JWKS to ensure validation service is ready
            var request = new GetJwksRequest();
            var result = await _requestSender.SendAsync(request, cancellationToken: cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to retrieve JWKS during initialization: {Error}", result.Error);
                throw new InvalidOperationException("Failed to initialize JWT client: Could not retrieve JWKS");
            }

            if (result.Value?.JwksJson == null)
            {
                _logger.LogError("Received empty JWKS during initialization");
                throw new InvalidOperationException("Failed to initialize JWT client: Empty JWKS received");
            }

            _logger.LogInformation("JWT client initialized successfully with JWKS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize JWT client");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JWT client stopping...");
        return Task.CompletedTask;
    }
}
