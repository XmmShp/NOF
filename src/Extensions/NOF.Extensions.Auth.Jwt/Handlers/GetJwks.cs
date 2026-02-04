namespace NOF;

/// <summary>
/// Handler for getting JWKS requests.
/// </summary>
public class GetJwks : IRequestHandler<GetJwksRequest, GetJwksResponse>
{
    private readonly IJwksService _jwksService;

    public GetJwks(IJwksService jwksService)
    {
        _jwksService = jwksService;
    }

    public async Task<Result<GetJwksResponse>> HandleAsync(GetJwksRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var jwksResponse = await _jwksService.GetJwksAsync(request.Audience, cancellationToken);
            return Result.Success(new GetJwksResponse(jwksResponse.Issuer, jwksResponse.Keys));
        }
        catch (Exception ex)
        {
            return Result.Fail(500, ex.Message);
        }
    }
}
