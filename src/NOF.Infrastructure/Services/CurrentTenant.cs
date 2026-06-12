using NOF.Abstraction;

namespace NOF.Infrastructure;

public sealed class CurrentTenant : IMutableCurrentTenant
{
    public string TenantId { get; private set; } = NOFAbstractionConstants.Tenant.HostId;

    public IDisposable PushTenant(string tenantId)
    {
        var previousTenantId = TenantId;
        TenantId = Infrastructure.TenantId.Normalize(tenantId);
        return new CurrentTenantScope(this, previousTenantId);
    }

    private sealed class CurrentTenantScope(CurrentTenant currentTenant, string previousTenantId) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            currentTenant.TenantId = previousTenantId;
            _disposed = true;
        }
    }
}
