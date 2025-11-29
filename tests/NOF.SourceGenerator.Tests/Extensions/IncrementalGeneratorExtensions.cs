using FluentAssertions;
using MassTransit.Mediator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace NOF.SourceGenerator.Tests;

internal static class IncrementalGeneratorExtensions
{
    extension(IIncrementalGenerator generator)
    {
        public GeneratorDriverRunResult GetResult(string source)
            => generator.GetResult(source);

        public GeneratorDriverRunResult GetResult<TAssembly>(string source)
            => generator.GetResult(source, typeof(TAssembly).Assembly);

        public GeneratorDriverRunResult GetResult(string source, params Assembly[] assemblies)
        {
            var compilation = CreateCompilation(source, assemblies);

            var driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

            driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            diagnostics.Should().BeEmpty("生成的代码应能成功编译");

            return driver.GetRunResult();
        }
    }

    public static CSharpCompilation CreateCompilation(string source, params Assembly[] extraAssemblies)
    {
        // 创建语法树
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // 创建引用
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location));

        var references = assemblies.Select(
                assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToList();

        // 添加必要的引用
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Request<>).Assembly.Location));
        references.AddRange(extraAssemblies.Select(assembly => MetadataReference.CreateFromFile(assembly.Location)));

        // 创建编译选项
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        // 创建编译
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: compilationOptions);

        return compilation;
    }
}
