using NOF.Hosting;

namespace NOF.Sample.Services;

[HttpRpcClient<IDemoDownstreamServiceClient>]
public partial class SelfHttpDemoDownstreamServiceClient;
