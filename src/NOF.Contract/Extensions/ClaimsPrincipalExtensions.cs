using System.Security.Claims;

namespace NOF.Contract;

public static partial class NOFContractExtensions
{
    extension(ClaimsPrincipal user)
    {
        /// <summary>
        /// Gets a value indicating whether the user is authenticated.
        /// </summary>
        public bool IsAuthenticated => user.Identity?.IsAuthenticated == true;

        /// <summary>
        /// Gets the unique identifier of the current user from the NameIdentifier claim.
        /// </summary>
        public string? Id => user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// Gets the username of the current user from the Name claim.
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
        /// Determines whether the current user has the specified permission.
        /// Supports exact match and wildcard patterns (for example: "order.*").
        /// </summary>
        public bool HasPermission(string permission, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(permission))
            {
                return false;
            }

            var permissions = user.Permissions;
            var comparer = StringComparer.FromComparison(comparison);
            if (permissions.Contains(permission, comparer))
            {
                return true;
            }

            return permissions
                .Where(pattern => pattern.Contains('*'))
                .Any(pattern => permission.MatchWildcard(pattern, comparison));
        }
    }
}
