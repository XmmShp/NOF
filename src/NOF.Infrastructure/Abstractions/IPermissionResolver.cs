using System.Security.Claims;

namespace NOF.Infrastructure;

/// <summary>
/// Resolves NOF permission values from token claims.
/// </summary>
public interface IPermissionResolver
{
    IReadOnlyCollection<string> ResolvePermissions(IReadOnlyCollection<Claim> claims);
}
