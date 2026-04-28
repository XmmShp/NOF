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
        typeof(Result<>)
    ];

    [Fact]
    public void ResultT_ReturnType_UsesResultFromInsteadOfDirectCast()
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
                                      global::System.Threading.Tasks.Task<Result<GetFleetOverviewResponse>> GetFleetOverviewAsync(
                                          Empty request,
                                          global::System.Threading.CancellationToken cancellationToken = default);
                                  }

                                  [LocalRpcClient<IDroneOpsServiceClient>]
                                  public partial class LocalDroneOpsClient;
                              }
                              """;

        var runResult = new LocalRpcClientGenerator().GetResultPostGen(source, _extraRefs);
        var code = GetGeneratedCode(runResult);

        Assert.Contains(
            "Result.From<global::MyApp.GetFleetOverviewResponse>((global::NOF.Contract.IResult)result!)",
            code);
        Assert.DoesNotContain(
            "(global::MyApp.Result<global::MyApp.GetFleetOverviewResponse>)result!",
            code);
        Assert.DoesNotContain(
            "(global::NOF.Contract.Result<global::MyApp.GetFleetOverviewResponse>)result!",
            code);
    }

    private static string GetGeneratedCode(Microsoft.CodeAnalysis.GeneratorDriverRunResult runResult)
        => runResult.GeneratedTrees
            .Select(tree => tree.GetRoot().ToFullString())
            .Single(code => code.Contains("partial class LocalDroneOpsClient"));
}
