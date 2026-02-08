namespace NOF;

/// <summary>
/// NOF DbContext 工厂选项
/// </summary>
public class DbContextFactoryOptions
{
    /// <summary>
    /// 是否自动迁移数据库
    /// </summary>
    public bool AutoMigrate { get; set; } = false;
}
