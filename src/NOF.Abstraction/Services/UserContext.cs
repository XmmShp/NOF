using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace NOF.Abstraction;

/// <summary>
/// 表示当前逻辑执行上下文中的用户信息。
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// 当前用户的 <see cref="ClaimsPrincipal"/>；可能为未认证用户。
    /// </summary>
    [AllowNull]
    ClaimsPrincipal User { get; set; }

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
    [AllowNull]
    public ClaimsPrincipal User
    {
        get;
        set
        {
            StateChanging?.Invoke();
            field = value ?? new();
            StateChanged?.Invoke();
        }
    } = new();
}

