using NOF.Application;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Handles <see cref="KeyRotationNotification"/> by immediately rotating the signing key.
/// This ensures all instances in a distributed deployment rotate keys in sync
/// when the notification is published via the NOF notification infrastructure.
/// </summary>
public class RotateSigningKey : INotificationHandler<KeyRotationNotification>
{
    private readonly ISigningKeyService _signingKeyService;

    public RotateSigningKey(ISigningKeyService signingKeyService)
    {
        _signingKeyService = signingKeyService;
    }

    public Task HandleAsync(KeyRotationNotification notification, CancellationToken cancellationToken)
    {
        _signingKeyService.RotateKey();
        return Task.CompletedTask;
    }
}
