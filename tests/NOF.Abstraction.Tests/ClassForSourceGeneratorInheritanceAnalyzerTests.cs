using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Abstraction;
using NOF.Abstraction.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ClassForSourceGeneratorInheritanceAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation(
            "TestAssembly",
            source,
            isDll: true,
            typeof(ClassForSourceGenerator).ToMetadataReference());

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ClassForSourceGeneratorInheritanceAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task SecondInheritanceLevel_IsAllowed()
    {
        const string source = """
            using NOF.Abstraction;
            namespace App;

            public abstract class A : ClassForSourceGenerator { }
            public class B : A { }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF401");
    }

    [Fact]
    public async Task ThirdInheritanceLevel_ReportsNOF401()
    {
        const string source = """
            using NOF.Abstraction;
            namespace App;

            public abstract class A : ClassForSourceGenerator { }
            public class B : A { }
            public class C : B { }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NOF401");
    }
}
