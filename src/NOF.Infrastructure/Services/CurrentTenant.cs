using NOF.Abstraction;

namespace NOF.Infrastructure;

public sealed class CurrentTenant : ICurrentTenant
{
    private static readonly AsyncLocal<TenantHolder?> Current = new();

    public string TenantId
    {
        get => Current.Value?.TenantId ?? NOFAbstractionConstants.Tenant.HostId;
        set
        {
            Current.Value = new TenantHolder
            {
                TenantId = NOF.Infrastructure.TenantId.Normalize(value)
            };
        }
    }

    public IDisposable Push(string tenantId)
    {
        var previous = TenantId;
        TenantId = tenantId;
        return new CurrentTenantScope(this, previous);
    }

    private sealed class TenantHolder
    {
        public string? TenantId { get; set; }
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
