using NOF.Annotation;
using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[AutoInject(Lifetime.Scoped, RegisterTypes = [typeof(JwtAuthorityService.GetJwks)])]
public sealed class GetJwks : JwtAuthorityService.GetJwks
{
    private readonly IJwksService _jwksService;

    public GetJwks(IJwksService jwksService)
    {
        _jwksService = jwksService;
    }

    public Task<Result<GetJwksResponse>> GetJwksAsync(GetJwksRequest request, CancellationToken cancellationToken = default)
    {
        var jwks = _jwksService.GetJwks();
        return Task.FromResult(Result.Success(new GetJwksResponse(jwks)));
    }
}
