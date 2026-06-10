using NOF.Contract;
using NOF.Contract.Extension.Authentication;

namespace NOF.Infrastructure.Extension.Authentication;

/// <summary>
/// Internal token service used by the OAuth/OIDC protocol surface.
/// </summary>
public interface ITokenService
{
    Task<Result<IssueTokenResponse>> IssueTokenAsync(IssueTokenRequest request, CancellationToken cancellationToken);

    Task<Result<ValidateRefreshTokenResponse>> ValidateRefreshTokenAsync(ValidateRefreshTokenRequest request, CancellationToken cancellationToken);

    Task<Result> RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken);
}
