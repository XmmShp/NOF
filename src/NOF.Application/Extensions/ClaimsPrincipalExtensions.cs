using System.Security.Claims;

namespace NOF;

public static partial class __NOF_Application_Extensions__
{
    extension(ClaimsPrincipal user)
    {
        /// <summary>
        /// Gets a value indicating whether the user is authenticated.
        /// </summary>
        public bool IsAuthenticated => user.Identity?.IsAuthenticated == true;

        /// <summary>
        /// Gets the unique identifier of the current user from the NameIdentifier claim, or <c>null</c> if not authenticated.
        /// </summary>
        public string? Id => user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// Gets the username of the current user from the Name claim, or <c>null</c> if not authenticated.
        /// </summary>
        public string? Name => user.Identity?.Name;

        /// <summary>
        /// Gets the list of permissions from the custom permission claims of the current user.
        /// </summary>
        public IReadOnlyList<string> Permissions => user.FindAll(ClaimTypes.Permission)
            .Select(claim => claim.Value)
            .ToList()
            .AsReadOnly();

        /// <summary>
        /// Determines whether the current user has the specified permission (role).
        /// Supports exact match and wildcard patterns (e.g., "order.*").
        /// </summary>
        /// <param name="permission">The permission name to check.</param>
        /// <param name="comparison">The string comparison type.</param>
        /// <returns><c>true</c> if the user has the permission; otherwise, <c>false</c>.</returns>
        public bool HasPermission(string permission, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(permission))
            {
                return false;
            }

            var permissions = user.Permissions;
            var comparer = StringComparer.FromComparison(comparison);

            // Exact match
            if (permissions.Contains(permission, comparer))
            {
                return true;
            }

            // Wildcard match (e.g., "order.*", "admin.*.delete")
            return permissions
                .Where(p => p.Contains('*'))
                .Any(pattern => permission.MatchWildcard(pattern, comparison));
        }
    }
}
