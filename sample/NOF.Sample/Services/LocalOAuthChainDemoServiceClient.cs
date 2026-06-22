using NOF.Infrastructure;

namespace NOF.Sample.Services;

[LocalRpcClient<IOAuthChainDemoServiceClient>]
public sealed partial class LocalOAuthChainDemoServiceClient;
