using System.Security.Claims;

namespace NOF.Contract;

/// <summary>
/// Represents the current user information for the active logical execution context.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Permission claim type used by NOF.
    /// </summary>
    public const string PermissionClaimType = "nof.permission";

    /// <summary>
    /// Tenant identifier claim type used by NOF.
    /// </summary>
    public const string TenantIdClaimType = "nof.tenant_id";

    /// <summary>
    /// The claims principal representing the current user.
    /// May be unauthenticated.
    /// </summary>
    ClaimsPrincipal User { get; }

    /// <summary>
    /// Occurs before the current user state changes.
    /// </summary>
    event Action? StateChanging;

    /// <summary>
    /// Occurs after the current user state has changed.
    /// </summary>
    event Action? StateChanged;

    void SetUser(ClaimsPrincipal user);

    void UnsetUser();
}

public static partial class NOFContractExtensions
{
    extension(IUserContext userContext)
    {
        /// <summary>
        /// Gets a value indicating whether the current user is authenticated.
        /// </summary>
        public bool IsAuthenticated
            => userContext.User.IsAuthenticated;

        /// <summary>
        /// Gets the unique identifier of the current user.
        /// </summary>
        public string? Id
            => userContext.User.Id;

        /// <summary>
        /// Gets the display name of the current user.
        /// </summary>
        public string? Name
            => userContext.User.Name;

        /// <summary>
        /// Gets the permissions granted to the current user.
        /// </summary>
        public IReadOnlyList<string> Permissions
            => userContext.User.Permissions;

        /// <summary>
        /// Gets the tenant identifier of the current user.
        /// </summary>
        public string? TenantId
            => userContext.User.TenantId;

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
