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

        /// <summary>
        /// Runs the generator and verifies that the post-generation compilation has no errors.
        /// Use this when the source deliberately has unimplemented interface members that the
        /// generator is expected to fill in (e.g. <c>IValueObject&lt;T&gt;</c>).
        /// </summary>
        public GeneratorDriverRunResult GetResultPostGen(string source, params Type[] types)
        {
            var extraReferences = types.Select(type => type.ToMetadataReference()).ToArray();
            var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);

            var driver = CSharpGeneratorDriver.Create(generator);

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation, out var outputCompilation, out _);

            var diagnostics = outputCompilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            diagnostics.Should().BeEmpty("Generated code should compile successfully");

            return driver.GetRunResult();
        }
    }
}
