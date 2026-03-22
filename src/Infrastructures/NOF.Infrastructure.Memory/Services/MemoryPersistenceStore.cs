using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryPersistenceStore : ICloneable
{
    private ConcurrentDictionary<string, MemoryPersistenceContext> TablesByTenant { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    internal void RestoreFrom(MemoryPersistenceStore snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        TablesByTenant = CloneTableDictionary(snapshot.TablesByTenant);
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
