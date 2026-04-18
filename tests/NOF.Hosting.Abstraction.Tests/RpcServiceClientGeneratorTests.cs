using Microsoft.CodeAnalysis.CSharp;
using NOF.Contract;
using NOF.Hosting;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcServiceClientGeneratorTests
{
    private static readonly Type[] _extraRefs =
    [
        typeof(HttpEndpointAttribute),
        typeof(IRpcClient),
        typeof(HttpRpcClientAttribute<>),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Empty),
        typeof(Result),
        typeof(Result<>),
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
        Assert.DoesNotContain("global::NOF.Application.IExecutionContext", code);
        Assert.Contains("foreach (var kvp in context.Headers)", code);
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
        Assert.Contains("Message = request,", code);
        Assert.Contains("GetDataAsync", code);
    }

    [Fact]
    public void BareReturnType_IsNormalizedToResultOfT()
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
                                      MyData GetData(GetDataRequest request);
                                  }

                                  public partial interface IMyServiceClient : IRpcClient;

                                  [HttpRpcClient<IMyServiceClient>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = RunGenerators(source);
        var code = GetGeneratedHttpClientCode(runResult);

        Assert.Contains("Task<global::NOF.Contract.Result<global::MyApp.MyData>> GetDataAsync", code);
        Assert.Contains("ReadFromJsonAsync<global::NOF.Contract.Result<global::MyApp.MyData>>", code);
    }

    private static Microsoft.CodeAnalysis.GeneratorDriverRunResult RunGenerators(string source)
    {
        var extraReferences = _extraRefs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);
        var driver = CSharpGeneratorDriver.Create(new NOF.Hosting.SourceGenerator.RpcServiceClientGenerator());

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
