using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryPersistenceStore : ICloneable
{
    private ConcurrentDictionary<string, MemoryPersistenceContext> TablesByTenant { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    internal void RestoreFrom(MemoryPersistenceStore snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var staleTenants = TablesByTenant.Keys.Except(snapshot.TablesByTenant.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var staleTenant in staleTenants)
        {
            TablesByTenant.TryRemove(staleTenant, out _);
        }

        foreach (var tenant in snapshot.TablesByTenant)
        {
            var targetContext = TablesByTenant.GetOrAdd(tenant.Key, static _ => new MemoryPersistenceContext());
            targetContext.RestoreFrom(tenant.Value);
        }
    }

    public static string NormalizeTenantId(string? tenantId)
        => string.IsNullOrWhiteSpace(tenantId) ? string.Empty : tenantId;

    public MemoryPersistenceContext CreateContext(string? tenantId)
        => TablesByTenant.GetOrAdd(NormalizeTenantId(tenantId), static _ => new MemoryPersistenceContext());

    public object Clone()
        => new MemoryPersistenceStore
        {
            TablesByTenant = CloneTableDictionary(TablesByTenant)
        };

    private static ConcurrentDictionary<string, MemoryPersistenceContext> CloneTableDictionary(
        ConcurrentDictionary<string, MemoryPersistenceContext> source)
    {
        var clone = new ConcurrentDictionary<string, MemoryPersistenceContext>(StringComparer.OrdinalIgnoreCase);

        foreach (var tenant in source)
        {
            clone[tenant.Key] = (MemoryPersistenceContext)tenant.Value.Clone();
        }

        return clone;
    }
}
