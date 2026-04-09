namespace NOF.Sample.Wasm.Services;

[Hosting.HttpServiceClient<INOFSampleService>]
public partial class HttpNOFSampleService : INOFSampleService
{
}
