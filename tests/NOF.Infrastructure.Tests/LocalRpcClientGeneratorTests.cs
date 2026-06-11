using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Contract;
using NOF.Infrastructure;
using NOF.Infrastructure.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class LocalRpcClientGeneratorTests
{
    private static readonly Type[] _extraRefs =
    [
        typeof(LocalRpcClientAttribute<>),
        typeof(IRpcClient),
        typeof(IRpcService),
        typeof(Result),
        typeof(Result<>),
        typeof(StreamingResult<>)
    ];

    [Fact]
    public void ResultT_ReturnType_UsesOuterTransportResult()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Infrastructure;

                              namespace MyApp
                              {
                                  public sealed record Empty;
                                  public sealed record GetFleetOverviewResponse(string Name);

                                  public partial interface IDroneOpsService : IRpcService
                                  {
                                      Result<GetFleetOverviewResponse> GetFleetOverview(Empty request);
                                  }

                                  public partial interface IDroneOpsServiceClient : IRpcClient
                                  {
                                      global::System.Threading.Tasks.Task<RpcResult<Result<GetFleetOverviewResponse>>> GetFleetOverviewAsync(
                                          Empty request,
                                          Context context,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  [LocalRpcClient<IDroneOpsServiceClient>]
                                  public partial class LocalDroneOpsClient;
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = GetGeneratedCode(runResult);

        Assert.Contains(
            "global::NOF.Contract.RpcResults.From<global::NOF.Contract.Result<global::MyApp.GetFleetOverviewResponse>>(result ?? throw new global::System.InvalidOperationException(\"RPC call returned a null response.\"))",
            code);
        Assert.DoesNotContain(
            "((global::NOF.Contract.IResult)result!)",
            code);
        Assert.DoesNotContain(
            "(global::MyApp.Result<global::MyApp.GetFleetOverviewResponse>)result!",
            code);
        Assert.DoesNotContain(
            "(global::NOF.Contract.Result<global::MyApp.GetFleetOverviewResponse>)result!",
            code);
    }

    [Fact]
    public void StreamingResult_ReturnType_UsesOuterTransportResult()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Infrastructure;
                              using System.Collections.Generic;

                              namespace MyApp
                              {
                                  public sealed record Empty;
                                  public sealed record DroneEvent(string Name);

                                  public partial interface IDroneOpsService : IRpcService
                                  {
                                      StreamingResult<DroneEvent> StreamEvents(Empty request);
                                  }

                                  public partial interface IDroneOpsServiceClient : IRpcClient
                                  {
                                      global::System.Threading.Tasks.Task<RpcResult<StreamingResult<DroneEvent>>> StreamEventsAsync(
                                          Empty request,
                                          Context context,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  [LocalRpcClient<IDroneOpsServiceClient>]
                                  public partial class LocalDroneOpsClient;
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = GetGeneratedCode(runResult);

        Assert.Contains("Task<global::NOF.Contract.RpcResult<global::NOF.Contract.StreamingResult<global::MyApp.DroneEvent>>> StreamEventsAsync", code);
        Assert.Contains(
            "global::NOF.Contract.RpcResults.From<global::NOF.Contract.StreamingResult<global::MyApp.DroneEvent>>(result ?? throw new global::System.InvalidOperationException(\"RPC call returned a null response.\"))",
            code);
    }

    [Fact]
    public void BareReturnType_UsesTransportResultOfBarePayload()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Infrastructure;

                              namespace MyApp
                              {
                                  public sealed record Empty;
                                  public sealed record GetFleetOverviewResponse(string Name);

                                  public partial interface IDroneOpsService : IRpcService
                                  {
                                      GetFleetOverviewResponse GetFleetOverview(Empty request);
                                  }

                                  public partial interface IDroneOpsServiceClient : IRpcClient
                                  {
                                      global::System.Threading.Tasks.Task<RpcResult<GetFleetOverviewResponse>> GetFleetOverviewAsync(
                                          Empty request,
                                          Context context,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  [LocalRpcClient<IDroneOpsServiceClient>]
                                  public partial class LocalDroneOpsClient;
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = GetGeneratedCode(runResult);

        Assert.Contains("Task<global::NOF.Contract.RpcResult<global::MyApp.GetFleetOverviewResponse>> GetFleetOverviewAsync", code);
        Assert.Contains(
            "global::NOF.Contract.RpcResults.From<global::MyApp.GetFleetOverviewResponse>(result ?? throw new global::System.InvalidOperationException(\"RPC call returned a null response.\"))",
            code);
    }

    private static string GetGeneratedCode(Microsoft.CodeAnalysis.GeneratorDriverRunResult runResult)
        => runResult.GeneratedTrees
            .Select(tree => tree.GetRoot().ToFullString())
            .Single(code => code.Contains("partial class LocalDroneOpsClient"));

    private static (GeneratorDriverRunResult Result, IReadOnlyList<Diagnostic> Diagnostics)
        RunGeneratorWithDiagnostics(string source)
    {
        var extraReferences = _extraRefs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

        var driver = CSharpGeneratorDriver.Create(new LocalRpcClientGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result = driver.GetRunResult();
        var diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToList();
        return (result, diagnostics);
    }
}
