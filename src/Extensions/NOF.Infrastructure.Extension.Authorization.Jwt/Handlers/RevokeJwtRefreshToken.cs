using NOF.Application;
using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Handler for revoking JWT refresh token requests.
/// This handler only revokes refresh tokens using their TokenId (jti).
/// Access tokens are short-lived and do not need revocation support.
/// </summary>
public class RevokeJwtRefreshToken : IRequestHandler<RevokeJwtRefreshTokenRequest>
{
    private readonly IRevokedRefreshTokenRepository _repository;

    public RevokeJwtRefreshToken(IRevokedRefreshTokenRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> HandleAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await _repository.RevokeAsync(request.TokenId, request.Expiration, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"An unexpected error occurred: {ex.Message}");
        }
    }
}
