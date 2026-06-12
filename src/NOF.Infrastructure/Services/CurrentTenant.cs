using NOF.Abstraction;

namespace NOF.Infrastructure;

public sealed class CurrentTenant : ICurrentTenant
{
    private string _tenantId = NOFAbstractionConstants.Tenant.HostId;

    public string TenantId
    {
        get => _tenantId;
        set => _tenantId = Infrastructure.TenantId.Normalize(value);
    }

    public IDisposable Push(string tenantId)
    {
        var previous = TenantId;
        TenantId = tenantId;
        return new CurrentTenantScope(this, previous);
    }

    private sealed class CurrentTenantScope(ICurrentTenant currentTenant, string previousTenantId) : IDisposable
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
