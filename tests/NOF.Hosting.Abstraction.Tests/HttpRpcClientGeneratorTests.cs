using Microsoft.CodeAnalysis.CSharp;
using NOF.Contract;
using NOF.Hosting;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class HttpRpcClientGeneratorTests
{
    private static readonly Type[] _extraRefs =
    [
        typeof(Abstraction.NOFAbstractionExtensions),
        typeof(HttpEndpointAttribute),
        typeof(IRpcClient),
        typeof(HttpRpcClientAttribute<>),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Empty),
        typeof(Result),
        typeof(Result<>),
        typeof(StreamingResult<>),
        typeof(HttpRpcTransportResultReader),
        typeof(System.Text.Json.JsonSerializerOptions),
        typeof(System.Net.Http.Json.JsonContent),
        typeof(System.Net.Http.Json.HttpContentJsonExtensions)
    ];

    [Fact]
    public void GeneratesHttpClient_OnAttributedPartialClass()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              namespace MyApp
                              {
                                  public record CreateUserRequest(string Name);

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Post, "/api/users")]
                                      Result CreateUser(CreateUserRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);
        Assert.Contains(": global::MyApp.IMyServiceClient", code);
        Assert.Contains("CreateUserAsync", code);
        Assert.Contains("HttpMethod.Post", code);
        Assert.DoesNotContain("global::NOF.Application.ITransparentInfos", code);
        Assert.Contains("global::NOF.Contract.Context context", code);
        Assert.Contains("new global::NOF.Hosting.RequestOutboundContext(context)", code);
        Assert.Contains("private static readonly global::System.Reflection.MethodInfo __CreateUserMethodInfo_0 =", code);
        Assert.Contains("MethodInfo = __CreateUserMethodInfo_0", code);
        Assert.Contains("foreach (var kvp in outboundContext.Headers)", code);
    }

    [Fact]
    public void MethodWithoutHttpEndpoint_DefaultsToPostAndOperationRoute()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              namespace MyApp
                              {
                                  public record InternalRequest(string Data);

                                  public partial interface IMyService : IRpcService
                                  {
                                      Result Internal(InternalRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);
        Assert.Contains("HttpMethod.Post", code);
        Assert.Contains("var endpoint = \"Internal\";", code);
    }

    [Fact]
    public void GetMethod_WithSingleRequestParam_ShouldGenerateMessageVariable()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              namespace MyApp
                              {
                                  public record MyData(string Value);
                                  public record GetDataRequest(string Key);

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Get, "/api/data")]
                                      Result<MyData> GetData(GetDataRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);
        Assert.Contains("ExecuteAsync(outboundContext, request, async (_, currentRequest, ct) =>", code);
        Assert.Contains("GetDataAsync", code);
    }

    [Fact]
    public void DeleteMethod_WithRequest_ShouldUseQueryStringInsteadOfBody()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              namespace MyApp
                              {
                                  public record DeleteUserRequest(string Name, int Age);

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Delete, "/api/users")]
                                      Result DeleteUser(DeleteUserRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);

        Assert.Contains("HttpMethod.Delete", code);
        Assert.Contains("var queryParts = new global::System.Collections.Generic.List<string>();", code);
        Assert.DoesNotContain("JsonContent.Create(request", code);
        Assert.DoesNotContain("httpRequest.Content =", code);
    }

    [Fact]
    public void CustomResult_ReturnType_IsReturnedDirectly()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              using System.Collections.Generic;
                              namespace MyApp
                              {
                                  public record GetDataRequest(string Key);
                                  public sealed record MyDataResult(string Value) : IResult
                                  {
                                      public bool IsSuccess => true;
                                      public string ErrorCode => string.Empty;
                                      public string Message => string.Empty;
                                      object? IResult.Value => Value;
                                      public IDictionary<string, string> Extra { get; } = new Dictionary<string, string>();
                                  }

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Get, "/api/data")]
                                      MyDataResult GetData(GetDataRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);

        Assert.Contains("Task<global::MyApp.MyDataResult> GetDataAsync", code);
        Assert.Contains("HttpRpcTransportResultReader.ReadAsync<global::MyApp.MyDataResult>", code);
        Assert.Contains("GetJsonTypeInfo<global::MyApp.MyDataResult>()", code);
        Assert.Contains("private static global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> GetJsonTypeInfo<T>()", code);
    }

    [Fact]
    public void ResultOfT_ReturnType_IsWrappedAsTransportResultOfBusinessResult()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              namespace MyApp
                              {
                                  public record MyData(string Value);
                                  public record GetDataRequest(string Key);

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Get, "/api/data")]
                                      Result<MyData> GetData(GetDataRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);

        Assert.Contains("Task<global::NOF.Contract.Result<global::MyApp.MyData>> GetDataAsync", code);
        Assert.Contains("HttpRpcTransportResultReader.ReadAsync<global::NOF.Contract.Result<global::MyApp.MyData>>", code);
    }

    [Fact]
    public void StreamReturnType_UsesStreamingResultAndSse()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              using System.Collections.Generic;

                              namespace MyApp
                              {
                                  public record StreamRequest(string Key);
                                  public record StreamEvent(string Value);

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Get, "/api/data/stream")]
                                      StreamingResult<StreamEvent> StreamData(StreamRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);

        Assert.Contains("Task<global::NOF.Contract.StreamingResult<global::MyApp.StreamEvent>> StreamDataAsync", code);
        Assert.Contains("text/event-stream", code);
        Assert.Contains("HttpCompletionOption.ResponseHeadersRead", code);
        Assert.Contains("SseResponseReader.ReadAsync<global::MyApp.StreamEvent>", code);
        Assert.Contains("result = global::NOF.Contract.Result.Stream<global::MyApp.StreamEvent>(stream);", code);
        Assert.Contains("HttpRpcTransportResultReader.ReadFailureAsync<global::NOF.Contract.StreamingResult<global::MyApp.StreamEvent>>", code);
        Assert.Contains("GetJsonTypeInfo<global::NOF.Contract.StreamingResult<global::MyApp.StreamEvent>>()", code);
    }

    private static Microsoft.CodeAnalysis.GeneratorDriverRunResult RunGenerators(string source)
    {
        var extraReferences = _extraRefs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);
        var driver = CSharpGeneratorDriver.Create(new Hosting.SourceGenerator.HttpRpcClientGenerator());

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var diagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(diagnostics);

        return driver.GetRunResult();
    }

    private static string GetGeneratedHttpClientCode(Microsoft.CodeAnalysis.GeneratorDriverRunResult runResult)
        => runResult.GeneratedTrees
            .Select(tree => tree.GetRoot().ToFullString())
            .Single(code => code.Contains("partial class MyServiceClient"));
}
