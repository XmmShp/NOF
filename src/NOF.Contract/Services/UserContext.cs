using System.Security.Claims;

namespace NOF.Contract;

/// <summary>
/// 表示当前逻辑执行上下文中的用户信息。
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// 当前用户的 <see cref="ClaimsPrincipal"/>；可能为未认证用户。
    /// </summary>
    ClaimsPrincipal User { get; }

    /// <summary>用户状态变更前触发。</summary>
    event Action? StateChanging;

    /// <summary>用户状态变更后触发。</summary>
    event Action? StateChanged;

    /// <summary>设置当前用户。</summary>
    void SetUser(ClaimsPrincipal user);

    /// <summary>清除当前用户，恢复为匿名用户。</summary>
    void UnsetUser();
}

public static partial class NOFContractExtensions
{
    extension(IUserContext userContext)
    {
        /// <summary>
        /// Gets a value indicating whether the current user is authenticated.
        /// </summary>
        public bool IsAuthenticated => userContext.User.IsAuthenticated;

        /// <summary>
        /// Gets the unique identifier of the current user.
        /// </summary>
        public string? Id => userContext.User.Id;

        /// <summary>
        /// Gets the display name of the current user.
        /// </summary>
        public string? Name => userContext.User.Name;

        /// <summary>
        /// Gets the permissions granted to the current user.
        /// </summary>
        public IReadOnlyList<string> Permissions => userContext.User.Permissions;

        /// <summary>
        /// Gets the tenant identifier of the current user.
        /// </summary>
        public string? TenantId => userContext.User.TenantId;

        /// <summary>
        /// Determines whether the current user has the specified permission.
        /// </summary>
        /// <param name="permission">The permission to check.</param>
        /// <param name="comparison">The string comparison used for matching.</param>
        /// <returns><c>true</c> if the user has the permission; otherwise, <c>false</c>.</returns>
        public bool HasPermission(string permission, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
            => userContext.User.HasPermission(permission, comparison);
    }
}

/// <summary>
/// 默认的 <see cref="IUserContext"/> 实现。
/// </summary>
public sealed class UserContext : IUserContext
{
    /// <summary>
    /// 共享的匿名用户。
    /// </summary>
    public static ClaimsPrincipal Anonymous { get; } = new();

    /// <inheritdoc />
    public event Action? StateChanging;

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <inheritdoc />
    public ClaimsPrincipal User { get; private set; } = Anonymous;

    /// <inheritdoc />
    public void SetUser(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        StateChanging?.Invoke();
        User = user;
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UnsetUser()
    {
        StateChanging?.Invoke();
        User = Anonymous;
        StateChanged?.Invoke();
    }
}
