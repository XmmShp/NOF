using NOF.Annotation;
using NOF.Application;
using NOF.Application.SourceGenerator;
using NOF.Contract;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcServerGeneratorTests
{
    [Fact]
    public void RpcServerAnalyzer_SupportedDiagnostics_ShouldOnlyContainPartialRule()
    {
        var analyzer = new RpcServerAnalyzer();
        var ids = analyzer.SupportedDiagnostics.Select(d => d.Id).ToArray();

        Assert.Equal(["NOF300"], ids);
    }

    [Fact]
    public void GeneratedCode_SplitsRpcInterfaceIntoPerMethodAbstractClasses()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;

            namespace App
            {
                public record PingRequest(string Value);
                public record GetRequest(int Id);
                public record ArchiveRequest(int Id);
                public record MyDto(string Value);

                public partial interface IMyService : IRpcService
                {
                    Result Ping(PingRequest request);
                    MyDto Get(GetRequest request);
                    Empty Archive(ArchiveRequest request);
                }

                public partial class MyService : RpcServer<IMyService>;
            }
            """;

        var result = new RpcServerGenerator().GetResultPostGen(source,
            typeof(RpcServer<>),
            typeof(RpcHandler<,>),
            typeof(AutoInjectAttribute),
            typeof(Empty),
            typeof(IRpcService),
            typeof(Result));
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("partial class MyService : global::NOF.Application.IRpcServer", generatedCode);
        Assert.Contains("protected override global::System.Collections.Generic.IReadOnlyDictionary<string, global::NOF.Application.RpcHandlerMapping> GetHandlerMappings()", generatedCode);
        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyDictionary<string, global::NOF.Application.RpcHandlerMapping> HandlerMappings => __rpcHandlerMappings;", generatedCode);
        Assert.Contains("public abstract class Ping : global::NOF.Application.RpcHandler<global::App.PingRequest, global::NOF.Contract.Result>", generatedCode);
        Assert.Contains("public abstract class Get : global::NOF.Application.RpcHandler<global::App.GetRequest, global::NOF.Contract.Result<global::App.MyDto>>", generatedCode);
        Assert.Contains("public abstract class Archive : global::NOF.Application.RpcHandler<global::App.ArchiveRequest, global::NOF.Contract.Result>", generatedCode);
        Assert.Contains("nameof(global::App.IMyService.Ping)] = new global::NOF.Application.RpcHandlerMapping(typeof(Ping), typeof(global::App.PingRequest), typeof(global::NOF.Contract.Result))", generatedCode);
        Assert.Contains("nameof(global::App.IMyService.Get)] = new global::NOF.Application.RpcHandlerMapping(typeof(Get), typeof(global::App.GetRequest), typeof(global::App.MyDto))", generatedCode);
        Assert.Contains("nameof(global::App.IMyService.Archive)] = new global::NOF.Application.RpcHandlerMapping(typeof(Archive), typeof(global::App.ArchiveRequest), typeof(global::NOF.Contract.Empty))", generatedCode);
    }
}
