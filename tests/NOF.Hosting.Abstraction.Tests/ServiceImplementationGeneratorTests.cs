using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Annotation;
using NOF.Application;
using NOF.Application.SourceGenerator;
using NOF.Contract;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ServiceImplementationGeneratorTests
{
    [Fact]
    public void GeneratedCode_RegistersServiceImplementationIntoAutoInjectRegistry()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App
            {
                public record PingRequest(string Value);

                public partial interface IMyService : IRpcService
                {
                    Task<Result> PingAsync(PingRequest request);
                }

                [ServiceImplementation<IMyService>]
                public partial class MyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(ServiceImplementationAttribute<>),
            typeof(AutoInjectAttribute),
            typeof(IRpcService),
            typeof(Result)
        );

        var result = new ServiceImplementationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("global::NOF.Annotation.AutoInjectRegistry.Register(typeof(global::App.IMyService), typeof(global::App.MyService), global::NOF.Annotation.Lifetime.Transient, useFactory: false);");
    }

    [Fact]
    public void GeneratedCode_StillRegistersServiceImplementation_WhenAutoInjectAlreadyExists()
    {
        const string source = """
            using NOF.Annotation;
            using NOF.Application;
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App
            {
                public record PingRequest(string Value);

                public partial interface IMyService : IRpcService
                {
                    Task<Result> PingAsync(PingRequest request);
                }

                [AutoInject(Lifetime.Scoped, RegisterTypes = [typeof(IMyService)])]
                [ServiceImplementation<IMyService>]
                public partial class MyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(ServiceImplementationAttribute<>),
            typeof(AutoInjectAttribute),
            typeof(IRpcService),
            typeof(Result)
        );

        var result = new ServiceImplementationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("global::NOF.Annotation.AutoInjectRegistry.Register(typeof(global::App.IMyService), typeof(global::App.MyService), global::NOF.Annotation.Lifetime.Transient, useFactory: false);");
    }

    [Fact]
    public void ServiceImplementationAnalyzer_SupportedDiagnostics_ShouldOnlyContainPartialRule()
    {
        var analyzer = new ServiceImplementationAnalyzer();
        var ids = analyzer.SupportedDiagnostics.Select(d => d.Id).ToArray();

        ids.Should().Equal("NOF300");
    }
}
