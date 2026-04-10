using Microsoft.CodeAnalysis.CSharp;
using NOF.Annotation;
using NOF.Application;
using NOF.Application.SourceGenerator;
using NOF.Contract;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class SplitInterfaceGeneratorTests
{
    [Fact]
    public void SplitInterfaceAnalyzer_SupportedDiagnostics_ShouldOnlyContainPartialRule()
    {
        var analyzer = new SplitInterfaceAnalyzer();
        var ids = analyzer.SupportedDiagnostics.Select(d => d.Id).ToArray();

        Assert.Equal(["NOF300"], ids);
    }

    [Fact]
    public void GeneratedCode_SplitsRpcInterfaceIntoPerMethodInterfaces()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            using System.Threading.Tasks;
            using System.Threading;

            namespace App
            {
                public record PingRequest(string Value);
                public record GetRequest(int Id);

                public partial interface IMyService : IRpcService
                {
                    Task<Result> PingAsync(PingRequest request);
                    Task<Result<string>> GetAsync(GetRequest request, string extra, CancellationToken cancellationToken = default);
                    Task<Result> DeleteAsync();
                }

                public partial class MyService : ISplitedInterface<IMyService>;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(ISplitedInterface<>),
            typeof(AutoInjectAttribute),
            typeof(IRpcService),
            typeof(Result)
        );

        var result = new SplitInterfaceGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("public interface Ping", generatedCode);
        Assert.Contains("public interface Get", generatedCode);
        Assert.Contains("public interface Delete", generatedCode);
        Assert.Contains("global::System.Threading.Tasks.Task<global::NOF.Contract.Result> PingAsync(", generatedCode);
        Assert.Contains("global::System.Threading.Tasks.Task<global::NOF.Contract.Result<string>> GetAsync(", generatedCode);
        Assert.Contains("global::System.Threading.Tasks.Task<global::NOF.Contract.Result> DeleteAsync();", generatedCode);
    }
}
