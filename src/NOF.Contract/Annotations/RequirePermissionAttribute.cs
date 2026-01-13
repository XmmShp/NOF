namespace NOF;

/// <summary>
/// 权限要求特性，用于标记页面组件需要的权限
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// 需要的权限，如果为null则表示只需要登录
    /// </summary>
    public string? Permission { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="permission">需要的权限，如果为null则表示只需要登录</param>
    public RequirePermissionAttribute(string? permission = null)
    {
        Permission = permission;
    }
}