using NOF.Contract.Extension.Authorization.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[LocalRpcClient<IJwtAuthorityServiceClient>]
public sealed partial class LocalJwtAuthorityServiceClient;
