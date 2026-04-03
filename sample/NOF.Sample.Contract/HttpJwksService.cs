using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;

namespace NOF.Sample;

[HttpServiceClient<IJwksService>]
public partial class HttpSampleJwksService;
