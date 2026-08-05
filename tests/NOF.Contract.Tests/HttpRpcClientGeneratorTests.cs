using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Contract.SourceGenerator;
using Xunit;

namespace NOF.Contract.Tests;

public class HttpRpcClientGeneratorTests
{
    private static readonly Type[] _refs =
    [
        typeof(System.Text.Json.NOFAbstractionExtensions),
        typeof(Empty),
        typeof(HttpEndpointAttribute),
        typeof(HttpRpcTransportResultReader),
        typeof(HttpClient),
        typeof(System.Net.Http.Json.JsonContent),
        typeof(System.Net.Http.Json.HttpContentJsonExtensions),
        typeof(System.Text.Json.JsonSerializerOptions),
        typeof(IRequestOutboundPipelineExecutor),
        typeof(IRpcClient),
        typeof(IRpcService),
        typeof(JsonRpcHttpClient),
        typeof(Result),
        typeof(Result<>),
        typeof(StreamingResult<>),
        typeof(TransportOverHttpAttribute)
    ];

    [Fact]
    public void TransportOverControllerRpc_GeneratesPublicHttpClientBesideContract()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record CreateUserRequest(string Name);

            [TransportOverHttp(HttpRpcStyle.ControllerRpc, "/api")]
            public interface IMyService : IRpcService
            {
                [HttpEndpoint(HttpVerb.Post, "/users")]
                Result CreateUser(CreateUserRequest request);
            }
            """;

        var code = GetGeneratedHttpClientCode(RunGenerators(source));

        Assert.Contains("public partial class HttpMyServiceClient : IMyServiceClient", code);
        Assert.Contains("public HttpMyServiceClient(global::System.Net.Http.HttpClient httpClient, global::NOF.Contract.IRequestOutboundPipelineExecutor? outboundPipeline = null)", code);
        Assert.Contains("var endpoint = \"/api/users\";", code);
        Assert.Contains("global::System.Net.Http.HttpMethod.Post", code);
        Assert.Contains("new global::NOF.Contract.RequestOutboundContext(context)", code);
        Assert.Contains("if (_outboundPipeline is null)", code);
        Assert.Contains("await dispatch(outboundContext, request, cancellationToken)", code);
        Assert.Contains("await _outboundPipeline.ExecuteAsync(outboundContext, request, dispatch, cancellationToken)", code);
        Assert.Contains("ResultProjection.RequireCompatible<global::NOF.Contract.Result>", code);
        Assert.DoesNotContain("global::NOF.Contract.JsonRpcHttpClient.SendAsync", code);
        Assert.DoesNotContain("global::NOF.Hosting", code);
    }

    [Fact]
    public void RpcServiceWithoutHttpTransport_DoesNotGenerateHttpClient()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record PingRequest(string Value);

            public interface IMyService : IRpcService
            {
                Result Ping(PingRequest request);
            }
            """;

        var runResult = RunGenerators(source);

        Assert.Contains(runResult.GeneratedTrees, tree => tree.GetRoot().ToFullString().Contains("interface IMyServiceClient"));
        Assert.DoesNotContain(runResult.GeneratedTrees, tree => tree.GetRoot().ToFullString().Contains("class HttpMyServiceClient"));
    }

    [Fact]
    public void ControllerRpcGet_UsesQueryStringAndStreamingUsesSse()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record QueryRequest(string Key, int? Page);
            public record StreamEvent(string Value);

            [TransportOverHttp(HttpRpcStyle.ControllerRpc)]
            public interface IMyService : IRpcService
            {
                [HttpEndpoint(HttpVerb.Get, "/events")]
                StreamingResult<StreamEvent> Stream(QueryRequest request);
            }
            """;

        var code = GetGeneratedHttpClientCode(RunGenerators(source));

        Assert.Contains("var queryParts = new global::System.Collections.Generic.List<string>();", code);
        Assert.Contains("var endpoint = \"/events\";", code);
        Assert.DoesNotContain("httpRequest.Content =", code);
        Assert.Contains("text/event-stream", code);
        Assert.Contains("global::NOF.Contract.SseResponseReader.ReadAsync", code);
        Assert.Contains("global::System.Net.Http.HttpCompletionOption.ResponseHeadersRead", code);
    }

    [Fact]
    public void TransportOverJsonRpc_GeneratesJsonRpcDispatchAndHonorsRoutePrefix()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record PingRequest(string Value);

            [TransportOverHttp(HttpRpcStyle.JsonRpc, "/contract-rpc")]
            public interface IMyService : IRpcService
            {
                Result Ping(PingRequest request);
            }
            """;

        var code = GetGeneratedHttpClientCode(RunGenerators(source));

        Assert.Contains("global::NOF.Contract.JsonRpcHttpClient.SendAsync", code);
        Assert.Contains("\"/contract-rpc\"", code);
        Assert.Contains("nameof(global::App.IMyService.Ping)", code);
        Assert.DoesNotContain("global::System.Net.Http.HttpRequestMessage", code);
    }

    [Fact]
    public void TransportOverJsonRpc_WithoutPrefix_UsesRootRoute()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record PingRequest(string Value);

            [TransportOverHttp(HttpRpcStyle.JsonRpc)]
            public interface IMyService : IRpcService
            {
                Result Ping(PingRequest request);
            }
            """;

        var code = GetGeneratedHttpClientCode(RunGenerators(source));

        Assert.Contains("\"/\"", code);
        Assert.DoesNotContain("\"/rpc\"", code);
    }

    [Fact]
    public void TransportOverJsonRpc_StreamingMethod_UsesJsonRpcSseClient()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record StreamRequest(string Value);
            public record StreamEvent(string Value);

            [TransportOverHttp(HttpRpcStyle.JsonRpc, "/stream-rpc")]
            public interface IMyService : IRpcService
            {
                StreamingResult<StreamEvent> Stream(StreamRequest request);
            }
            """;

        var code = GetGeneratedHttpClientCode(RunGenerators(source));

        Assert.Contains("JsonRpcHttpClient.SendStreamingAsync<global::App.StreamRequest, global::App.StreamEvent>", code);
        Assert.Contains("GetJsonTypeInfo<global::App.StreamEvent>()", code);
        Assert.Contains("GetJsonTypeInfo<global::NOF.Contract.Result>()", code);
        Assert.DoesNotContain("JsonRpcHttpClient.SendAsync<global::App.StreamRequest, global::NOF.Contract.StreamingResult", code);
    }

    [Fact]
    public void GenericRpcService_GeneratesGenericHttpClientWithConstraints()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record Query<TValue>(TValue Value);

            [TransportOverHttp(HttpRpcStyle.ControllerRpc)]
            public interface IMyService<TValue> : IRpcService
                where TValue : class, new()
            {
                Result<TValue> Get(Query<TValue> request);
            }
            """;

        var code = GetGeneratedHttpClientCode(RunGenerators(source));

        Assert.Contains("public partial class HttpMyServiceClient<TValue> : IMyServiceClient<TValue>", code);
        Assert.Contains("where TValue : class, new()", code);
        Assert.Contains("public HttpMyServiceClient(global::System.Net.Http.HttpClient httpClient", code);
    }

    [Fact]
    public void HttpClientGenerator_DoesNotRequireRpcServiceClientGeneratorOutputAsInput()
    {
        const string source = """
            using NOF.Contract;

            namespace App;

            public record PingRequest(string Value);

            [TransportOverHttp(HttpRpcStyle.JsonRpc)]
            public interface IMyService : IRpcService
            {
                Result Ping(PingRequest request);
            }
            """;

        var extraReferences = _refs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpRpcClientGenerator());

        driver = driver.RunGenerators(compilation);

        var code = GetGeneratedHttpClientCode(driver.GetRunResult());
        Assert.Contains("partial class HttpMyServiceClient : IMyServiceClient", code);
        Assert.Contains("Task<global::NOF.Contract.Result> PingAsync", code);
    }

    private static GeneratorDriverRunResult RunGenerators(string source)
    {
        var extraReferences = _refs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new RpcServiceClientGenerator().AsSourceGenerator(),
            new HttpRpcClientGenerator().AsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var diagnostics = outputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(diagnostics);

        return driver.GetRunResult();
    }

    private static string GetGeneratedHttpClientCode(GeneratorDriverRunResult runResult)
        => runResult.GeneratedTrees
            .Select(tree => tree.GetRoot().ToFullString())
            .Single(code => code.Contains("partial class HttpMyServiceClient"));
}
