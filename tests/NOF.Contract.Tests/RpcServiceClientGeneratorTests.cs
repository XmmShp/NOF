using NOF.Contract.SourceGenerator;
using NOF.SourceGenerator.Tests;
using Xunit;

namespace NOF.Contract.Tests;

public class RpcServiceClientGeneratorTests
{
    private static readonly Type[] _refs =
    [
        typeof(Empty),
        typeof(HttpEndpointAttribute),
        typeof(IRpcClient),
        typeof(IRpcService),
        typeof(Result),
        typeof(Result<>),
        typeof(StreamingResult<>)
    ];

    [Fact]
    public void GeneratesClientInterface_WithTransportWrappedReturnTypes()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record PingRequest(string Value);
            public record GetRequest(int Id);
            public record ArchiveRequest(int Id);
            public record Pong(string Value);

            public partial interface IMyService : IRpcService
            {
                Result Ping(PingRequest request);
                Pong Get(GetRequest request);
                Empty Archive(ArchiveRequest request);
            }
            """;

        var runResult = new RpcServiceClientGenerator().GetResult(source, _refs);
        var code = runResult.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("public interface IMyServiceClient : global::NOF.Contract.IRpcClient", code);
        Assert.Contains("global::System.Threading.Tasks.Task<global::NOF.Contract.RpcResult<global::NOF.Contract.Result>> PingAsync", code);
        Assert.Contains("global::System.Threading.Tasks.Task<global::NOF.Contract.RpcResult<global::App.Pong>> GetAsync", code);
        Assert.Contains("global::System.Threading.Tasks.Task<global::NOF.Contract.RpcResult<global::NOF.Contract.Empty>> ArchiveAsync", code);
    }

    [Fact]
    public void GeneratesClientInterface_WithStreamingResultForStreamingMethods()
    {
        const string source = """
            using NOF.Contract;
            using System.Collections.Generic;

            namespace App;

            public record StreamRequest(string Value);
            public record StreamEvent(string Value);

            public partial interface IMyService : IRpcService
            {
                StreamingResult<StreamEvent> Stream(StreamRequest request);
            }
            """;

        var runResult = new RpcServiceClientGenerator().GetResult(source, _refs);
        var code = runResult.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("global::System.Threading.Tasks.Task<global::NOF.Contract.RpcResult<global::NOF.Contract.StreamingResult<global::App.StreamEvent>>> StreamAsync", code);
    }
}
