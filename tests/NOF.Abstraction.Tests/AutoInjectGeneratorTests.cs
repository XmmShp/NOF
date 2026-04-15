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
        Assert.Single(trees);

        var appRoot = trees.Single().GetRoot();
        var appNs = appRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Single();
        Assert.Equal("App",
        appNs.Name.ToString());

        var generatedCode = appRoot.ToFullString();
        Assert.Contains("class __AppAutoInjectAssemblyInitializer", generatedCode);
        Assert.Contains("AppService", generatedCode);
        Assert.DoesNotContain("LibService", generatedCode);
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
        Assert.Single(trees);

        var generatedCode = trees.Single().GetRoot().ToFullString();
        Assert.Contains("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppAutoInjectAssemblyInitializer>]", generatedCode);
        Assert.Contains("global::NOF.Abstraction.Registry.AutoInjectRegistrations.Add", generatedCode);
        Assert.Contains("new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.IAppSvc), typeof(global::App.AppService), global::NOF.Annotation.Lifetime.Scoped, false)", generatedCode);
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

        Assert.DoesNotContain("AddAppAutoInjectServices", generatedCode);
        Assert.DoesNotContain("IServiceCollection", generatedCode);
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

        Assert.Contains("Registry.AutoInjectRegistrations.Add", generatedCode);
        Assert.Contains("new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.FooBar), typeof(global::App.FooBar), global::NOF.Annotation.Lifetime.Singleton, false)", generatedCode);
        Assert.Contains("new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.IFoo), typeof(global::App.FooBar), global::NOF.Annotation.Lifetime.Singleton, true)", generatedCode);
        Assert.Contains("new global::NOF.Annotation.AutoInjectServiceRegistration(typeof(global::App.IBar), typeof(global::App.FooBar), global::NOF.Annotation.Lifetime.Singleton, true)", generatedCode);
    }
}
