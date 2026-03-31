using NOF.Annotation;
using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[AutoInject(Lifetime.Scoped, RegisterTypes = new[] { typeof(JwtAuthorityService.RevokeJwtRefreshToken) })]
public sealed class RevokeJwtRefreshToken : JwtAuthorityService.RevokeJwtRefreshToken
{
    private readonly IRevokedRefreshTokenRepository _revokedRefreshTokenRepository;

    public RevokeJwtRefreshToken(IRevokedRefreshTokenRepository revokedRefreshTokenRepository)
    {
        _revokedRefreshTokenRepository = revokedRefreshTokenRepository;
    }

    public async Task<Result> RevokeJwtRefreshTokenAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        await _revokedRefreshTokenRepository
            .RevokeAsync(request.TokenId, request.Expiration, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
