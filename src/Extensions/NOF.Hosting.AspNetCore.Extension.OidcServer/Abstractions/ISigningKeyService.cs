namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface ISigningKeyService
{
    Task<ManagedSigningKey> GetCurrentSigningKeyAsync(CancellationToken cancellationToken = default);

    Task<ManagedSigningKey[]> GetAllKeysAsync(CancellationToken cancellationToken = default);

    Task RotateKeyAsync(CancellationToken cancellationToken = default);
}
