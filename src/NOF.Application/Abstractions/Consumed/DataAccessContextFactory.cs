namespace NOF;

/// <summary>
/// 租户数据访问上下文工厂接口
/// </summary>
public interface IDataAccessContextFactory
{
    IDataAccessContext CreateTenantContext();
    IDataAccessContext CreateTenantContext(string tenantId);
}

/// <summary>
/// 公共数据访问上下文工厂接口
/// </summary>
public interface IPublicDataAccessContextFactory
{
    IDataAccessContext CreatePublicContext();
}
