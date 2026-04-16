using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Abstraction;
using NOF.Annotation;
using NOF.Application;
using NOF.Application.SourceGenerator;
using NOF.Contract;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcServerAutoInjectGeneratorTests
{
    [Fact]
    public void GeneratedCode_RegistersRpcServerArtifacts()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            using System.Threading;
            using System.Threading.Tasks;

            namespace App
            {
                public record PingRequest(string Value);

                public partial interface IMyService : IRpcService
                {
                    Result Ping(PingRequest request);
                }

                public partial class MyService : RpcServer<IMyService>;

                public sealed class PingHandler : MyService.Ping
                {
                    public override Task<Result> HandleAsync(PingRequest request, CancellationToken cancellationToken)
                        => Task.FromResult(Result.Success());
                }
            }
            """;

        var compilation = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(RpcServer<>),
            typeof(RpcHandler<,>),
            typeof(IRpcService),
            typeof(Result),
            typeof(Result<>),
            typeof(Registry),
            typeof(AutoInjectServiceRegistration),
            typeof(AssemblyInitializeAttribute),
            typeof(RpcServerRegistration));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([
            new RpcServerGenerator().AsSourceGenerator(),
            new RpcServerAutoInjectGenerator().AsSourceGenerator()
        ]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = string.Join("\n\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.GetRoot().ToFullString()));

        Assert.Contains("AssemblyInitializeAttribute<global::App.__AppRpcServerAutoInjectAssemblyInitializer>", generatedCode);
        Assert.Contains("Registry.AutoInjectRegistrations.Add(new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.MyService), typeof(global::App.MyService), global::NOF.Annotation.Lifetime.Scoped, false));", generatedCode);
        Assert.Contains("Registry.RpcServerRegistrations.Add(new global::NOF.Application.RpcServerRegistration(typeof(global::App.IMyService), typeof(global::App.MyService)));", generatedCode);
        Assert.Contains("Registry.AutoInjectRegistrations.Add(new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.MyService.Ping), typeof(global::App.PingHandler), global::NOF.Annotation.Lifetime.Transient, false));", generatedCode);
    }

    [Fact]
    public void GeneratedCode_DoesNotRegister_UnrelatedInterfaces()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;

            namespace App
            {
                public partial interface IMyService : IRpcService
                {
                    Result Run(RunRequest request);
                }

                public record RunRequest(string Value);

                public partial class MyService : RpcServer<IMyService>;

                public interface IUnrelated
                {
                    Result Run(RunRequest request);
                }

                public sealed class UnrelatedHandler : IUnrelated
                {
                    public Result Run(RunRequest request) => Result.Success();
                }
            }
            """;

        var compilation = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(RpcServer<>),
            typeof(RpcHandler<,>),
            typeof(IRpcService),
            typeof(Result),
            typeof(Result<>),
            typeof(Registry),
            typeof(AutoInjectServiceRegistration),
            typeof(AssemblyInitializeAttribute),
            typeof(RpcServerRegistration));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([
            new RpcServerGenerator().AsSourceGenerator(),
            new RpcServerAutoInjectGenerator().AsSourceGenerator()
        ]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = string.Join("\n\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.GetRoot().ToFullString()));
        Assert.DoesNotContain("UnrelatedHandler", generatedCode);
    }
}
