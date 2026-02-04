using Microsoft.Extensions.Options;

namespace NOF;

/// <summary>
/// Handler for getting JWKS requests.
/// </summary>
public class GetJwks : IRequestHandler<GetJwksRequest, GetJwksResponse>
{
    private readonly IJwksService _jwksService;
    private readonly JwtOptions _jwtOptions;

    public GetJwks(IJwksService jwksService, IOptions<JwtOptions> jwtOptions)
    {
        _jwksService = jwksService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<GetJwksResponse>> HandleAsync(GetJwksRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await _jwksService.GetJwksAsync(request.Audience, cancellationToken);
            return Result.Success(new GetJwksResponse(_jwtOptions.Issuer, keys));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, ex.Message);
        }
    }
}
