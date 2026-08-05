using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using NOF.Infrastructure.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class LocalRpcClientGeneratorTests
{
    private static readonly Type[] _extraRefs =
    [
        typeof(RpcServer<>),
        typeof(RpcServerInvoker),
        typeof(IRpcClient),
        typeof(IRpcClient<>),
        typeof(IRpcService),
        typeof(Result),
        typeof(Result<>),
        typeof(StreamingResult<>)
    ];

    [Fact]
    public void ResultT_ReturnType_UsesDirectResult()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Application;
                              using NOF.Infrastructure;

                              namespace MyApp
                              {
                                  public sealed record Empty;
                                  public sealed record GetFleetOverviewResponse(string Name);

                                  public partial interface IDroneOpsService : IRpcService
                                  {
                                      Result<GetFleetOverviewResponse> GetFleetOverview(Empty request);
                                  }

                                  public partial interface IDroneOpsServiceClient : IRpcClient<IDroneOpsService>
                                  {
                                      global::System.Threading.Tasks.Task<Result<GetFleetOverviewResponse>> GetFleetOverviewAsync(
                                          Empty request,
                                          Context context,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  public abstract class DroneOpsServer : RpcServer<IDroneOpsService>;
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = GetGeneratedCode(runResult);

        Assert.Contains(
            "global::NOF.Contract.ResultProjection.RequireCompatible<global::NOF.Contract.Result<global::MyApp.GetFleetOverviewResponse>>(result ?? throw new global::System.InvalidOperationException(\"RPC call returned a null response.\"))",
            code);
        Assert.Contains("public sealed class LocalDroneOpsServerClient : global::MyApp.IDroneOpsServiceClient", code);
        Assert.Contains("private static readonly global::System.Reflection.MethodInfo __GetFleetOverviewMethodInfo_0 =", code);
        Assert.Contains("RpcServerInvoker.InvokeAsync<global::MyApp.IDroneOpsService>(_serviceProvider, __GetFleetOverviewMethodInfo_0, request, context, cancellationToken)", code);
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
    public void StreamingResult_ReturnType_UsesDirectResult()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Application;
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

                                  public partial interface IDroneOpsServiceClient : IRpcClient<IDroneOpsService>
                                  {
                                      global::System.Threading.Tasks.Task<StreamingResult<DroneEvent>> StreamEventsAsync(
                                          Empty request,
                                          Context context,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  public abstract class DroneOpsServer : RpcServer<IDroneOpsService>;
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = GetGeneratedCode(runResult);

        Assert.Contains("Task<global::NOF.Contract.StreamingResult<global::MyApp.DroneEvent>> StreamEventsAsync", code);
        Assert.Contains(
            "global::NOF.Contract.ResultProjection.RequireCompatible<global::NOF.Contract.StreamingResult<global::MyApp.DroneEvent>>(result ?? throw new global::System.InvalidOperationException(\"RPC call returned a null response.\"))",
            code);
    }

    [Fact]
    public void CustomResult_ReturnType_UsesDirectResult()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Application;
                              using NOF.Infrastructure;
                              using System.Collections.Generic;

                              namespace MyApp
                              {
                                  public sealed record Empty;
                                  public sealed record FleetOverviewResult(string Name) : IResult
                                  {
                                      public bool IsSuccess => true;
                                      public string ErrorCode => string.Empty;
                                      public string Message => string.Empty;
                                      public object? Value => Name;
                                      public IDictionary<string, string> Extra { get; } = new Dictionary<string, string>();
                                  }

                                  public partial interface IDroneOpsService : IRpcService
                                  {
                                      FleetOverviewResult GetFleetOverview(Empty request);
                                  }

                                  public partial interface IDroneOpsServiceClient : IRpcClient<IDroneOpsService>
                                  {
                                      global::System.Threading.Tasks.Task<FleetOverviewResult> GetFleetOverviewAsync(
                                          Empty request,
                                          Context context,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  public abstract class DroneOpsServer : RpcServer<IDroneOpsService>;
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = GetGeneratedCode(runResult);

        Assert.Contains("Task<global::MyApp.FleetOverviewResult> GetFleetOverviewAsync", code);
        Assert.Contains(
            "global::NOF.Contract.ResultProjection.RequireCompatible<global::MyApp.FleetOverviewResult>(result ?? throw new global::System.InvalidOperationException(\"RPC call returned a null response.\"))",
            code);
    }

    [Fact]
    public void GeneratesLocalClient_ForGenericRpcClient()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Application;
                              using NOF.Infrastructure;

                              namespace MyApp
                              {
                                  public sealed class Payload
                                  {
                                  }

                                  public sealed record Query<TValue>(TValue Value);

                                  public partial interface IMyService<TValue> : IRpcService
                                      where TValue : class, new()
                                  {
                                      Result<TValue> Get(Query<TValue> request);
                                  }

                                  public partial interface IMyServiceClient<TValue> : IRpcClient<IMyService<TValue>>
                                      where TValue : class, new()
                                  {
                                      global::System.Threading.Tasks.Task<Result<TValue>> GetAsync(
                                          Query<TValue> request,
                                          Context context,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  public abstract class MyServiceServer<TValue> : RpcServer<IMyService<TValue>>
                                      where TValue : class, new();
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = runResult.GeneratedTrees
            .Select(tree => tree.GetRoot().ToFullString())
            .Single(generated => generated.Contains("class LocalMyServiceServerClient<TValue>"));

        Assert.Contains("public sealed class LocalMyServiceServerClient<TValue> : global::MyApp.IMyServiceClient<TValue>", code);
        Assert.Contains("where TValue : class, new()", code);
        Assert.Contains("RpcServerInvoker.InvokeAsync<global::MyApp.IMyService<TValue>>", code);
    }

    [Fact]
    public void RpcServer_WithoutClientGeneratorOutput_StillProducesLocalClient()
    {
        const string source = """
                              using NOF.Application;
                              using NOF.Contract;

                              namespace MyApp
                              {
                                  public sealed record Empty;

                                  public interface IDroneOpsService : IRpcService
                                  {
                                      Result Ping(Empty request);
                                  }

                                  public abstract class DroneOpsServer : RpcServer<IDroneOpsService>;
                              }
                              """;

        var extraReferences = _extraRefs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new LocalRpcClientGenerator());

        driver = driver.RunGenerators(compilation);

        var code = GetGeneratedCode(driver.GetRunResult());
        Assert.Contains("public sealed class LocalDroneOpsServerClient : global::MyApp.IDroneOpsServiceClient", code);
        Assert.Contains("Task<global::NOF.Contract.Result> PingAsync", code);
    }

    private static string GetGeneratedCode(GeneratorDriverRunResult runResult)
        => runResult.GeneratedTrees
            .Select(tree => tree.GetRoot().ToFullString())
            .Single(code => code.Contains("class LocalDroneOpsServerClient"));

}
