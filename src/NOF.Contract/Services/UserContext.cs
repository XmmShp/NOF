using DotNet.Globbing;

namespace NOF;

/// <summary>
/// 用户上下文接口，用于获取当前用户信息
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// 用户是否已认证
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 用户ID
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// 用户名
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// 用户权限列表
    /// </summary>
    List<string> Permissions { get; }

    /// <summary>
    /// 检查用户是否拥有指定权限
    /// </summary>
    /// <param name="permission">权限名称</param>
    /// <returns>如果用户拥有该权限则返回true，否则返回false</returns>
    bool HasPermission(string permission);

    /// <summary>
    /// 设置当前用户上下文
    /// </summary>
    /// <param name="id">用户id</param>
    /// <param name="username">用户名</param>
    /// <param name="permissions">用户权限</param>
    void SetUser(string id, string username, IEnumerable<string> permissions);

    /// <summary>
    /// 设置当前用户上下文
    /// </summary>
    /// <param name="id">用户id</param>
    /// <param name="username">用户名</param>
    /// <param name="permissions">用户权限</param>
    Task SetUserAsync(string id, string username, IEnumerable<string> permissions);

    /// <summary>
    /// 取消设置当前用户上下文，即设为登出状态
    /// </summary>
    void UnsetUser();

    /// <summary>
    /// 取消设置当前用户上下文，即设为登出状态
    /// </summary>
    Task UnsetUserAsync();
}

/// <summary>
/// 用户上下文实现，用于获取当前用户信息
/// </summary>
public class UserContext : IUserContext
{
    public bool IsAuthenticated => Id is not null;

    public string? Id { get; private set; }

    public string? Username { get; private set; }

    public List<string> Permissions { get; } = [];

    public bool HasPermission(string permission)
    {
        // 精确匹配
        if (Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // 通配符匹配
        return Permissions.Where(userPermission => userPermission.Contains('*'))
            .Select(Glob.Parse)
            .Any(glob => glob.IsMatch(permission));
    }

    public void SetUser(string id, string username, IEnumerable<string> permissions)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidOperationException("用户ID不可为空");
        }

        if (string.IsNullOrEmpty(username))
        {
            throw new InvalidOperationException("用户名不可为空");
        }
        Id = id;
        Username = username;
        Permissions.AddRange(permissions);
    }

    public Task SetUserAsync(string id, string username, IEnumerable<string> permissions)
        => Task.Run(() => SetUser(id, username, permissions));

    public void UnsetUser()
    {
        Id = null;
        Username = null;
        Permissions.Clear();
    }

    public Task UnsetUserAsync()
        => Task.Run(UnsetUser);
}
