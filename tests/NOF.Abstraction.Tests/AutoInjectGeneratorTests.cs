using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Abstraction.SourceGenerator;
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
            using NOF.Abstraction;
            namespace App.Lib
            {
                public interface ILibSvc { }
                [AutoInject(ServiceLifetime.Singleton)]
                public class LibService : ILibSvc { }
            }
            """;

        var depRefs = new[]
        {
            typeof(IServiceCollection).ToMetadataReference(),
            typeof(AutoInjectAttribute).ToMetadataReference(),
            typeof(AssemblyInitializationServices).ToMetadataReference(),
            typeof(InitializedTypes).ToMetadataReference()
        };
        var libComp = CSharpCompilation.CreateCompilation("App.Lib", libSource, isDll: true, depRefs);
        var libRef = libComp.CreateMetadataReference();

        const string mainSource = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Abstraction;
            namespace App
            {
                public interface IAppSvc { }
                [AutoInject(ServiceLifetime.Transient)]
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
            using NOF.Abstraction;
            namespace App
            {
                public interface IAppSvc { }
                [AutoInject(ServiceLifetime.Scoped)]
                public class AppService : IAppSvc { }
            }
            """;

        var depRefs = new[]
        {
            typeof(IServiceCollection).ToMetadataReference(),
            typeof(AutoInjectAttribute).ToMetadataReference(),
            typeof(AssemblyInitializationServices).ToMetadataReference(),
            typeof(InitializedTypes).ToMetadataReference()
        };
        var compilation = CSharpCompilation.CreateCompilation("App", source, isDll: true, depRefs);

        var result = new AutoInjectGenerator().GetResult(compilation);
        var trees = result.GeneratedTrees;
        Assert.Single(trees);

        var generatedCode = trees.Single().GetRoot().ToFullString();
        const string scopedInterfaceDescriptor = "services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Describe(typeof(global::App.IAppSvc), typeof(global::App.AppService), global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped));";

        Assert.Contains("[assembly: global::NOF.Abstraction.AssemblyInitializeAttribute<global::App.__AppAutoInjectAssemblyInitializer>]", generatedCode);
        Assert.Contains("services.InitializedTypes.Add(typeof(__AppAutoInjectAssemblyInitializer))", generatedCode);
        Assert.Contains(scopedInterfaceDescriptor, generatedCode);
        Assert.DoesNotContain("AutoInjectServiceRegistration", generatedCode);
        Assert.DoesNotContain("ServiceProviderServiceExtensions.GetRequiredService(sp, typeof(global::App.AppService))", generatedCode);
        Assert.DoesNotContain("ServiceDescriptor.Describe(typeof(global::App.AppService), typeof(global::App.AppService)", generatedCode);
        Assert.Equal(1, CountOccurrences(generatedCode, "services.Add("));
    }

    [Fact]
    public void GeneratedCode_DoesNotGenerateLegacyAddAutoInjectServicesMethod()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Abstraction;
            namespace App
            {
                public interface IMyService { }
                [AutoInject(ServiceLifetime.Scoped)]
                public class MyService : IMyService { }
            }
            """;

        var depRefs = new[]
        {
            typeof(IServiceCollection).ToMetadataReference(),
            typeof(AutoInjectAttribute).ToMetadataReference(),
            typeof(AssemblyInitializationServices).ToMetadataReference(),
            typeof(InitializedTypes).ToMetadataReference()
        };
        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, depRefs);

        var result = new AutoInjectGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.DoesNotContain("AddAppAutoInjectServices", generatedCode);
        Assert.Contains("IServiceCollection services", generatedCode);
    }

    [Fact]
    public void GeneratedCode_ExplicitRegisterTypes_ArePersistedInRegistryMetadata()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using NOF.Abstraction;
            namespace App
            {
                public interface IFoo { }
                public interface IBar { }
                [AutoInject(ServiceLifetime.Singleton, RegisterTypes = [typeof(IFoo), typeof(IBar)])]
                public class FooBar : IFoo, IBar { }
            }
            """;

        var depRefs = new[]
        {
            typeof(IServiceCollection).ToMetadataReference(),
            typeof(AutoInjectAttribute).ToMetadataReference(),
            typeof(AssemblyInitializationServices).ToMetadataReference(),
            typeof(InitializedTypes).ToMetadataReference()
        };
        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, depRefs);

        var result = new AutoInjectGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();
        const string selfDescriptor = "services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Describe(typeof(global::App.FooBar), typeof(global::App.FooBar), global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton));";
        const string fooFactoryDescriptor = "services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Describe(typeof(global::App.IFoo), sp => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService(sp, typeof(global::App.FooBar)), global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton));";
        const string barFactoryDescriptor = "services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Describe(typeof(global::App.IBar), sp => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService(sp, typeof(global::App.FooBar)), global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton));";

        Assert.Contains(selfDescriptor, generatedCode);
        Assert.Contains(fooFactoryDescriptor, generatedCode);
        Assert.Contains(barFactoryDescriptor, generatedCode);
        Assert.DoesNotContain("AutoInjectServiceRegistration", generatedCode);
        Assert.Equal(3, CountOccurrences(generatedCode, "services.Add("));
        Assert.Equal(2, CountOccurrences(generatedCode, "ServiceProviderServiceExtensions.GetRequiredService(sp, typeof(global::App.FooBar))"));
        Assert.Equal(3, CountOccurrences(generatedCode, "global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton"));
        Assert.DoesNotContain("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped", generatedCode);
        Assert.DoesNotContain("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient", generatedCode);
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;
}
