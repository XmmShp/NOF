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

public class SplitInterfaceAutoInjectGeneratorTests
{
    [Fact]
    public void GeneratedCode_RegistersSplitOperationImplementation_AsTransient()
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
                    Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken = default);
                }

                public partial class MyService : ISplitedInterface<IMyService>;

                public sealed class PingHandler : MyService.Ping
                {
                    public Task<Result> PingAsync(PingRequest request, CancellationToken cancellationToken)
                        => Task.FromResult(Result.Success());
                }
            }
            """;

        var compilation = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(ISplitedInterface<>),
            typeof(IRpcService),
            typeof(Result),
            typeof(Result<>),
            typeof(Registry),
            typeof(AutoInjectServiceRegistration),
            typeof(AssemblyInitializeAttribute));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([
            new SplitInterfaceGenerator().AsSourceGenerator(),
            new SplitInterfaceAutoInjectGenerator().AsSourceGenerator()
        ]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = string.Join("\n\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.GetRoot().ToFullString()));

        Assert.Contains("AssemblyInitializeAttribute<global::App.__AppSplitInterfaceAutoInjectAssemblyInitializer>", generatedCode);
        Assert.Contains("Registry.AutoInjectRegistrations.Add(new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.MyService.Ping), typeof(global::App.PingHandler), global::NOF.Annotation.Lifetime.Transient, false));", generatedCode);
    }

    [Fact]
    public void GeneratedCode_DoesNotRegister_UnrelatedInterfaces()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App
            {
                public partial interface IMyService : IRpcService
                {
                    Task<Result> RunAsync();
                }

                public partial class MyService : ISplitedInterface<IMyService>;

                public interface IUnrelated
                {
                    Task<Result> RunAsync();
                }

                public sealed class UnrelatedHandler : IUnrelated
                {
                    public Task<Result> RunAsync() => Task.FromResult(Result.Success());
                }
            }
            """;

        var compilation = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(ISplitedInterface<>),
            typeof(IRpcService),
            typeof(Result),
            typeof(Result<>),
            typeof(Registry),
            typeof(AutoInjectServiceRegistration),
            typeof(AssemblyInitializeAttribute));

        var result = new SplitInterfaceAutoInjectGenerator().GetResult(compilation);

        Assert.Empty(result.GeneratedTrees);
    }
}
