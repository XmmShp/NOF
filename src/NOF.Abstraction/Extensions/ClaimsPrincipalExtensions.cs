namespace System.Security.Claims;

public static partial class NOFAbstractionExtensions
{
    private const string JwtSubjectClaimType = "sub";
    private const string JwtNameClaimType = "name";
    private const string JwtEmailClaimType = "email";
    private const string JwtSessionIdClaimType = "sid";

    extension(ClaimsPrincipal user)
    {
        /// <summary>
        /// Gets a value indicating whether the user is authenticated.
        /// </summary>
        public bool IsAuthenticated => user.Identities.Any(identity => identity.IsAuthenticated);

        /// <summary>
        /// Gets the unique identifier of the current user from the JWT subject claim.
        /// </summary>
        public string? Id => user.FindFirst(JwtSubjectClaimType)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// Gets the username of the current user from the standard JWT name claim.
        /// </summary>
        public string? Name => user.FindFirst(JwtNameClaimType)?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.Identities
            .Where(identity => identity.IsAuthenticated)
            .Select(identity => identity.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? user.Identity?.Name;

        /// <summary>
        /// Gets the email of the current user from the standard JWT email claim.
        /// </summary>
        public string? Email => user.FindFirst(JwtEmailClaimType)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value;

        /// <summary>
        /// Gets the session identifier of the current user from the standard JWT sid claim.
        /// </summary>
        public string? SessionId => user.FindFirst(JwtSessionIdClaimType)?.Value
            ?? user.FindFirst(ClaimTypes.Sid)?.Value;

        /// <summary>
        /// Gets the list of permissions from the custom permission claims of the current user.
        /// </summary>
        public IReadOnlyList<string> Permissions => user.FindAll(ClaimTypes.Permission)
            .Select(claim => claim.Value)
            .ToList()
            .AsReadOnly();

        /// <summary>
        /// Gets the tenant identifier of the current user from the NOF tenant claim.
        /// </summary>
        public string? TenantId => user.FindFirst(ClaimTypes.TenantId)?.Value;

        /// <summary>
        /// Gets the proxy service name from token exchange claims. Returns null when the current token was not proxied.
        /// </summary>
        public string? ProxyServiceName => user.FindFirst(ClaimTypes.ProxyServiceName)?.Value;

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

        /// <summary>
        /// Gets the first identity of the specified type from the current user.
        /// </summary>
        public TIdentity? GetIdentity<TIdentity>() where TIdentity : ClaimsIdentity
        {
            return user.Identities.OfType<TIdentity>().FirstOrDefault();
        }

        /// <summary>
        /// Gets all identities of the specified type from the current user.
        /// </summary>
        public IReadOnlyList<TIdentity> GetIdentities<TIdentity>() where TIdentity : ClaimsIdentity
        {
            return user.Identities.OfType<TIdentity>().ToList().AsReadOnly();
        }
    }
}
