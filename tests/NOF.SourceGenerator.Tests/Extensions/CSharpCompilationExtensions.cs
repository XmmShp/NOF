using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace NOF.SourceGenerator.Tests.Extensions;

public static class CSharpCompilationExtensions
{
    private static readonly Assembly[] AppDomainAssemblies;

    static CSharpCompilationExtensions()
    {
        AppDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location)).ToArray();
    }

    extension(CSharpCompilation compilation)
    {
        public static CSharpCompilation CreateCompilation(string assemblyName, string source, bool isDll)
            => CSharpCompilation.CreateCompilation(assemblyName, source, isDll, additionalRefs: []);

        public static CSharpCompilation CreateCompilation(string assemblyName, string source, bool isDll, params Type[] types)
            => CSharpCompilation.CreateCompilation(assemblyName, source, isDll, types.Select(type => type.ToMetadataReference()).ToArray());

        public static CSharpCompilation CreateCompilation(string assemblyName, string source, bool isDll, params MetadataReference[] additionalRefs)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var references = AppDomainAssemblies.Select(
                    assembly => MetadataReference.CreateFromFile(assembly.Location))
                .Cast<MetadataReference>()
                .ToList();
            references.AddRange(additionalRefs);

            return CSharpCompilation.Create(
                assemblyName,
                [syntaxTree],
                references,
                new CSharpCompilationOptions(isDll ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication));
        }

        public MetadataReference CreateMetadataReference()
        {
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            result.Success.Should().BeTrue($"Compilation of {compilation.AssemblyName} failed: {string.Join(", ", result.Diagnostics)}");
            ms.Position = 0;
            return MetadataReference.CreateFromStream(ms);
        }
    }
}
