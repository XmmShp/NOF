using NOF.Contract;
using NOF.Infrastructure.Extension.Authorization.Jwt;

namespace NOF.Sample;

[GenerateService(
    Namespaces = ["NOF.Sample"],
    ExtraTypes =
    [
        typeof(GenerateJwtTokenRequest),
        typeof(ValidateJwtRefreshTokenRequest),
        typeof(RevokeJwtRefreshTokenRequest),
        typeof(GetJwksRequest)
    ])]
public partial interface INOFSampleService;
