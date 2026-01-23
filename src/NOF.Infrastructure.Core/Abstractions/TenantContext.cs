namespace NOF;

/// <summary>
/// 租户上下文接口，用于标识当前操作的租户
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// 当前租户标识符
    /// </summary>
    string CurrentTenantId { get; }
}

/// <summary>
/// 租户提供者接口，扩展了基本的租户提供功能
/// </summary>
public interface ITenantContextInternal : ITenantContext
{
    /// <summary>
    /// 设置当前租户标识符
    /// </summary>
    /// <param name="tenantId">租户标识符</param>
    void SetCurrentTenantId(string tenantId);
}

/// <summary>
/// 租户上下文的默认实现
/// </summary>
public class TenantContext : ITenantContextInternal
{
    public string CurrentTenantId { get; private set; } = "default";

    public void SetCurrentTenantId(string tenantId)
    {
        CurrentTenantId = tenantId;
    }
}

public static partial class NOFConstants
{
    public const string TenantId = "NOF.Tenant.TenantId";
}