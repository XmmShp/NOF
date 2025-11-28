namespace NOF;

/// <summary>
/// 标记权限常量的特性，用于自动生成权限信息
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class PermissionAttribute : Attribute
{
    /// <summary>
    /// 权限描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 创建权限特性实例
    /// </summary>
    /// <param name="description">权限描述</param>
    public PermissionAttribute(string description)
    {
        Description = description;
    }
}
