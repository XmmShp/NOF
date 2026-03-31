using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction.SourceGenerator;
using NOF.Annotation;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class AutoInjectGeneratorTests
{
    [Fact]
    public void GenerateInitializer_UsesOnlyCurrentAssemblyTypes()
    {
        const string libSource = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Annotation;
            namespace App.Lib
            {
                public interface ILibSvc { }
                [AutoInject(Lifetime.Singleton)]
                public class LibService : ILibSvc { }
            }
            """;

        var depRefs = new[] { typeof(IServiceCollection).ToMetadataReference(), typeof(AutoInjectAttribute).ToMetadataReference() };
        var libComp = CSharpCompilation.CreateCompilation("App.Lib", libSource, isDll: true, depRefs);
        var libRef = libComp.CreateMetadataReference();

        const string mainSource = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Annotation;
            namespace App
            {
                public interface IAppSvc { }
                [AutoInject(Lifetime.Transient)]
                public class AppService : IAppSvc { }
            }
            """;

        var mainComp = CSharpCompilation.CreateCompilation("App", mainSource, isDll: true, [libRef, .. depRefs]);

        var result = new AutoInjectGenerator().GetResult(mainComp);
        var trees = result.GeneratedTrees;
        trees.Should().ContainSingle();

        var appRoot = trees.Single().GetRoot();
        var appNs = appRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Single();
        appNs.Name.ToString().Should().Be("App");

        var generatedCode = appRoot.ToFullString();
        generatedCode.Should().Contain("class __AppAutoInjectAssemblyInitializer");
        generatedCode.Should().Contain("AppService");
        generatedCode.Should().NotContain("LibService");
    }

    [Fact]
    public void GeneratedCode_ContainsAssemblyInitializerAttribute_AndRegistryRegistration()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Annotation;
            namespace App
            {
                public interface IAppSvc { }
                [AutoInject(Lifetime.Scoped)]
                public class AppService : IAppSvc { }
            }
            """;

        var depRefs = new[] { typeof(IServiceCollection).ToMetadataReference(), typeof(AutoInjectAttribute).ToMetadataReference() };
        var compilation = CSharpCompilation.CreateCompilation("App", source, isDll: true, depRefs);

        var result = new AutoInjectGenerator().GetResult(compilation);
        var trees = result.GeneratedTrees;
        trees.Should().ContainSingle();

        var generatedCode = trees.Single().GetRoot().ToFullString();
        generatedCode.Should().Contain("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppAutoInjectAssemblyInitializer>]");
        generatedCode.Should().Contain("global::NOF.Annotation.AutoInjectRegistry.Register");
        generatedCode.Should().Contain("typeof(global::App.IAppSvc), typeof(global::App.AppService), global::NOF.Annotation.Lifetime.Scoped, useFactory: false");
    }

    [Fact]
    public void GeneratedCode_DoesNotGenerateLegacyAddAutoInjectServicesMethod()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Annotation;
            namespace App
            {
                public interface IMyService { }
                [AutoInject(Lifetime.Scoped)]
                public class MyService : IMyService { }
            }
            """;

        var depRefs = new[] { typeof(IServiceCollection).ToMetadataReference(), typeof(AutoInjectAttribute).ToMetadataReference() };
        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, depRefs);

        var result = new AutoInjectGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().NotContain("AddAppAutoInjectServices");
        generatedCode.Should().NotContain("IServiceCollection");
    }

    [Fact]
    public void GeneratedCode_ExplicitRegisterTypes_ArePersistedInRegistryMetadata()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Annotation;
            namespace App
            {
                public interface IFoo { }
                public interface IBar { }
                [AutoInject(Lifetime.Singleton, RegisterTypes = [typeof(IFoo), typeof(IBar)])]
                public class FooBar : IFoo, IBar { }
            }
            """;

        var depRefs = new[] { typeof(IServiceCollection).ToMetadataReference(), typeof(AutoInjectAttribute).ToMetadataReference() };
        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, depRefs);

        var result = new AutoInjectGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("typeof(global::App.FooBar), typeof(global::App.FooBar), global::NOF.Annotation.Lifetime.Singleton, useFactory: false");
        generatedCode.Should().Contain("typeof(global::App.IFoo)");
        generatedCode.Should().Contain("typeof(global::App.IBar)");
        generatedCode.Should().Contain("useFactory: true");
    }
}
