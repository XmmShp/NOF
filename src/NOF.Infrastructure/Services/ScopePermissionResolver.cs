using System.Security.Claims;

namespace NOF.Infrastructure;

/// <summary>
/// Default permission resolver that maps each scope claim to the same permission value.
/// </summary>
public sealed class ScopePermissionResolver : IPermissionResolver
{
    private const string ScopeClaimType = "scope";

    public IReadOnlyCollection<string> ResolvePermissions(IReadOnlyCollection<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        return claims
            .Where(static claim => string.Equals(claim.Type, ScopeClaimType, StringComparison.Ordinal))
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
