namespace NOF.Infrastructure;

public interface ICurrentTenant
{
    string TenantId { get; }
}

public interface IMutableCurrentTenant : ICurrentTenant
{
    IDisposable PushTenant(string tenantId);
}
