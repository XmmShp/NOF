using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NOF.Annotation;
using NOF.Hosting.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class AutoInjectGeneratorTests
{
    [Fact]
    public void GenerateServiceCollectionExtensions_ForClassesInBothMainAndReferenced_Assemblies_CombinesAll()
    {
        // --- 类库 ---
        const string libSource = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Annotation;
            namespace Lib
            {
                public interface ILibSvc { }
                [AutoInject(Lifetime.Singleton)]
                public class LibService : ILibSvc { }
            }
            """;

        var depRefs = new[] { typeof(IServiceCollection).ToMetadataReference(), typeof(AutoInjectAttribute).ToMetadataReference() };
        var libComp = CSharpCompilation.CreateCompilation("Lib", libSource, isDll: true, depRefs);
        var libRef = libComp.CreateMetadataReference();

        // --- 主项目 ---
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

        // --- 运行生成器 ---
        var result = new AutoInjectGenerator().GetResult(mainComp);
        var trees = result.GeneratedTrees;
        trees.Should().ContainSingle();

        // 检查 App 的生成
        var appRoot = trees.Single().GetRoot();
        var appNs = appRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Single();
        appNs.Name.ToString().Should().Be("App");

        var appMethod = appNs.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        appMethod.Identifier.Text.Should().Be("AddAppAutoInjectServices");
        appMethod.Body!.ToString().Should().Contain("IAppSvc").And.Contain("AppService").And.Contain("ServiceLifetime.Transient");
        appMethod.Body!.ToString().Should().Contain("ILibSvc").And.Contain("LibService").And.Contain("ServiceLifetime.Singleton");
    }

    [Fact]
    public void GeneratedCode_UsesFqnForServiceDescriptorAndServiceLifetime()
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

        // Should use global:: FQN for ServiceDescriptor
        generatedCode.Should().Contain("global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor");
        // Should use global:: FQN for ServiceLifetime
        generatedCode.Should().Contain("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped");
        // Should use global:: FQN for IServiceCollection in method signature
        generatedCode.Should().Contain("global::Microsoft.Extensions.DependencyInjection.IServiceCollection");
        // Should NOT have bare using Microsoft.Extensions.DependencyInjection at the top
        // (we allow it for GetRequiredService extension method)
        generatedCode.Should().Contain("using Microsoft.Extensions.DependencyInjection;");
    }

    [Fact]
    public void GeneratedCode_SingletonWithMultipleInterfaces_UsesGetRequiredServiceExtension()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Annotation;
            namespace App
            {
                public interface IFoo { }
                public interface IBar { }
                [AutoInject(Lifetime.Singleton)]
                public class FooBar : IFoo, IBar { }
            }
            """;

        var depRefs = new[] { typeof(IServiceCollection).ToMetadataReference(), typeof(AutoInjectAttribute).ToMetadataReference() };
        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, depRefs);

        var result = new AutoInjectGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Singleton with multiple interfaces: register self, then factory delegates
        generatedCode.Should().Contain("sp.GetRequiredService<");
        // Self registration
        generatedCode.Should().Contain("typeof(App.FooBar), typeof(App.FooBar)");
    }
}
