using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.SourceGenerator.Tests.Extensions;

namespace NOF.SourceGenerator.Tests;

internal static class IncrementalGeneratorExtensions
{
    extension(IIncrementalGenerator generator)
    {
        public GeneratorDriverRunResult GetResult(string source, params Type[] types)
        {
            var extraReferences = types.Select(type => type.ToMetadataReference()).ToArray();
            var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

            return generator.GetResult(compilation);
        }

        public GeneratorDriverRunResult GetResult(CSharpCompilation compilation)
        {
            var driver = CSharpGeneratorDriver.Create(generator);

            driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            diagnostics.Should().BeEmpty("Generated code should compile successfully");

            return driver.GetRunResult();
        }
    }
}
