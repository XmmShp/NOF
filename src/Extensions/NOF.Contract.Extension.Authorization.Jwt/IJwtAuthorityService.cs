using NOF.Contract;

namespace NOF.Contract.Extension.Authorization.Jwt;

/// <summary>
/// Internal service for JWT authority operations.
/// </summary>
public interface IJwtAuthorityService : IRpcService
{
	/// <summary>
	/// Generates a new JWT access token and refresh token pair.
	/// </summary>
	/// <param name="request">The token generation request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The generated token pair.</returns>
	Task<Result<GenerateJwtTokenResponse>> GenerateJwtTokenAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Validates a refresh token and returns its claims.
	/// </summary>
	/// <param name="request">The refresh token validation request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The validation result with token claims.</returns>
	Task<Result<ValidateJwtRefreshTokenResponse>> ValidateJwtRefreshTokenAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Revokes a refresh token.
	/// </summary>
	/// <param name="request">The token revocation request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The revocation result.</returns>
	Task<Result> RevokeJwtRefreshTokenAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken = default);
}