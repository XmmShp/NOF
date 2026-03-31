using NOF.Contract;
using NOF.Infrastructure.Extension.Authorization.Jwt;

namespace NOF.Sample;

[HttpServiceClient<IJwtAuthorityService>]
public partial class HttpSampleJwtAuthorityService;
