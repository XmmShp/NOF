using NOF.Abstraction;
using NOF.Contract;

namespace NOF.Infrastructure;

internal static class TransactionalMessageTenants
{
    public static async IAsyncEnumerable<string> EnumerateAsync(
        ITransactionalMessageTenantProvider provider,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hostId = TenantId.Normalize(null);
        seen.Add(hostId);
        yield return hostId;

        await foreach (var tenantId in provider.GetTenantIdsAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            var normalized = TenantId.Normalize(tenantId);
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    public static IDisposable Push(string tenantId)
        => Context.PushCurrent(Context.Empty.WithTenantId(tenantId));
}
