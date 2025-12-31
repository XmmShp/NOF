using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
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
            using NOF;
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
            using NOF;
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
        appNs.Name.ToString().Should().Be("NOF.Generated");

        var appMethod = appNs.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        appMethod.Identifier.Text.Should().Be("AddAppAutoInjectServices");
        appMethod.Body!.ToString().Should().Contain("IAppSvc").And.Contain("AppService").And.Contain("ServiceLifetime.Transient");
        appMethod.Body!.ToString().Should().Contain("ILibSvc").And.Contain("LibService").And.Contain("ServiceLifetime.Singleton");
    }
}