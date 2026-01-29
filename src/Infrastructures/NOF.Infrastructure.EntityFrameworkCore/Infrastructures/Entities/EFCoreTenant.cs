namespace NOF;

/// <summary>
/// EFCore 租户实体
/// </summary>
internal sealed class EFCoreTenant
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
