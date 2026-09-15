namespace NOF.Infrastructure;

/// <summary>
/// Enumerates the tenants whose databases contain transactional inbox and outbox tables.
/// Implementations should read a durable tenant catalog so pending messages remain discoverable after a restart.
/// The host tenant is included by the message processors automatically.
/// </summary>
public interface ITransactionalMessageTenantProvider
{
    IAsyncEnumerable<string> GetTenantIdsAsync(CancellationToken cancellationToken = default);
}
