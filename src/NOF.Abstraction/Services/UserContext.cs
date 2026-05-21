using System.Security.Claims;

namespace NOF.Abstraction;

/// <summary>
/// 表示当前逻辑执行上下文中的用户信息。
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// 当前用户的固定主体；外部仅应通过增删 Identity 修改用户状态。
    /// </summary>
    ClaimsPrincipal User { get; }

    /// <summary>
    /// 退出当前登录状态并清空全部身份。
    /// </summary>
    void Logout();

    /// <summary>用户状态变更前触发。</summary>
    event Action? StateChanging;

    /// <summary>用户状态变更后触发。</summary>
    event Action? StateChanged;
}

/// <summary>
/// 默认的 <see cref="IUserContext"/> 实现。
/// </summary>
public sealed class UserContext : IUserContext
{
    /// <inheritdoc />
    public event Action? StateChanging;

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <inheritdoc />
    public ClaimsPrincipal User { get; private set; }

    public UserContext()
    {
        User = CreateUser();
    }

    /// <inheritdoc />
    public void Logout()
    {
        StateChanging?.Invoke();
        User = CreateUser();
        StateChanged?.Invoke();
    }

    private UserClaimsPrincipal CreateUser()
    {
        return new UserClaimsPrincipal(
            () => StateChanging?.Invoke(),
            () => StateChanged?.Invoke());
    }
}
