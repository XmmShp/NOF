using System.ComponentModel;

namespace NOF;

/// <summary>
/// 租户仓储接口，支持对租户的增删改查操作
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITenantRepository
{
    /// <summary>
    /// 根据租户ID获取租户信息
    /// </summary>
    /// <param name="tenantId">租户标识符</param>
    /// <returns>租户信息，如果不存在则返回null</returns>
    ValueTask<Tenant?> FindAsync(string tenantId);

    /// <summary>
    /// 获取所有租户信息
    /// </summary>
    /// <returns>所有租户的列表</returns>
    ValueTask<IReadOnlyList<Tenant>> GetAllAsync();

    /// <summary>
    /// 添加新租户
    /// </summary>
    /// <param name="tenant">要添加的租户信息</param>
    void Add(Tenant tenant);

    /// <summary>
    /// 更新租户信息
    /// </summary>
    /// <param name="tenant">要更新的租户信息</param>
    void Update(Tenant tenant);

    /// <summary>
    /// 删除租户（根据租户ID删除）
    /// </summary>
    /// <param name="tenantId">要删除的租户标识符</param>
    void Delete(string tenantId);

    /// <summary>
    /// 移除租户（根据租户实体删除）
    /// </summary>
    /// <param name="tenant">要移除的租户实体</param>
    void Remove(Tenant tenant);

    /// <summary>
    /// 检查租户是否存在
    /// </summary>
    /// <param name="tenantId">租户标识符</param>
    /// <returns>租户是否存在</returns>
    ValueTask<bool> ExistsAsync(string tenantId);
}

/// <summary>
/// 租户实体
/// </summary>
public class Tenant
{
    /// <summary>
    /// 租户标识符
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 租户名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 租户描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 租户是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
