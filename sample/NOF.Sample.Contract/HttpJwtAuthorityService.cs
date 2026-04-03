using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;

namespace NOF.Sample;

[HttpServiceClient<IJwtAuthorityService>]
public partial class HttpSampleJwtAuthorityService;
