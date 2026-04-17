using NOF.Contract.Extension.Authorization.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[LocalRpcClient<IJwksServiceClient>]
public sealed partial class LocalJwksServiceClient;
