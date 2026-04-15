using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Abstraction.SourceGenerator;
using NOF.Annotation;
using NOF.SourceGenerator.Tests.Extensions;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class SourceGeneratorAttributeInheritanceAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation(
            "TestAssembly",
            source,
            isDll: true,
            typeof(AttributeForSourceGenerator).ToMetadataReference());

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new SourceGeneratorAttributeInheritanceAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task NonSealedAttributeForSourceGenerator_ReportsNOF400()
    {
        const string source = """
            using NOF.Annotation;

            namespace App;

            public class DemoAttribute : AttributeForSourceGenerator
            {
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NOF400");
    }

    [Fact]
    public async Task SealedAttributeForSourceGenerator_DoesNotReportNOF400()
    {
        const string source = """
            using NOF.Annotation;

            namespace App;

            public sealed class DemoAttribute : AttributeForSourceGenerator
            {
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF400");
    }
}
