using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting;

namespace NOF.Sample.UI.Services;

[HttpRpcClient<IJwtAuthorityServiceClient>]
public partial class HttpJwtAuthorityService;

