namespace NOF.Infrastructure;

public interface ICurrentTenant
{
    string TenantId { get; set; }

    IDisposable Push(string tenantId);
}
