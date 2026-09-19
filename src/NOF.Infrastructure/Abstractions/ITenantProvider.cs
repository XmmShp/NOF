namespace NOF.Infrastructure;

/// <summary>
/// Enumerates tenants for infrastructure operations, including database migrations and transactional messaging.
/// Implementations should read a durable tenant catalog and be safe for concurrent enumeration.
/// Register implementations as singletons; create a scope when accessing scoped catalog services.
/// Consumers handle the host tenant automatically, so implementations may omit it.
/// </summary>
public interface ITenantProvider
{
    /// <summary>Returns the tenant identifiers to process, observing cancellation.</summary>
    IAsyncEnumerable<string> GetTenantIdsAsync(CancellationToken cancellationToken = default);
}
