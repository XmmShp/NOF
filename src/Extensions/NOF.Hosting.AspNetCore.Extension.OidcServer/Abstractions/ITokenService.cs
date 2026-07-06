using NOF.Contract;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public interface ITokenService
{
    Task<Result<IssueTokenResponse>> IssueTokenAsync(IssueTokenRequest request, CancellationToken cancellationToken);

    Task<Result<ValidateRefreshTokenResponse>> ValidateRefreshTokenAsync(ValidateRefreshTokenRequest request, CancellationToken cancellationToken);

    Task<Result> RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken);

    Task<Result<IntrospectTokenResponse>> IntrospectTokenAsync(IntrospectTokenRequest request, CancellationToken cancellationToken);
}
