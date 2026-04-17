using NOF.Infrastructure;

namespace NOF.Sample.Services;

[LocalRpcClient<INOFSampleServiceClient>]
public sealed partial class LocalNOFSampleServiceClient;

