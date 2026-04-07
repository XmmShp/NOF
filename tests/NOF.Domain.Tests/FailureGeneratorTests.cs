using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NOF.Domain;
using NOF.Domain.SourceGenerator;
using NOF.SourceGenerator.Tests;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class FailureGeneratorTests
{
    [Fact]
    public void GenerateFailureClass_ForSingleClass_GeneratesCorrectCode()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [Failure("InvalidInput", "invalid input", "1001")]
                [Failure("NotFound", "not found", "1002")]
                public partial class MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Single(result.GeneratedTrees);

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var ns = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Single();
        Assert.Equal("Test", ns.Name.ToString());

        var classDecl = ns.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        Assert.Equal("MyFailure", classDecl.Identifier.Text);

        var fields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
        Assert.Equal(2, fields.Count);

        var invalidInputField = fields.First(f => f.Declaration.Variables.First().Identifier.Text == "InvalidInput");
        Assert.Contains("new(\"invalid input\", \"1001\")", invalidInputField.ToString());

        var notFoundField = fields.First(f => f.Declaration.Variables.First().Identifier.Text == "NotFound");
        Assert.Contains("new(\"not found\", \"1002\")", notFoundField.ToString());
    }

    [Fact]
    public void GenerateFailureClass_ForPartialClassesInSameNamespace_MergesAndGeneratesSingleFile()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [Failure("InvalidInput", "invalid input", "1001")]
                public partial class MyFailure
                {
                }

                [Failure("NotFound", "not found", "1002")]
                public partial class MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Single(result.GeneratedTrees);

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

        var fields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
        Assert.Equal(2, fields.Count);

        var fieldNames = fields.Select(f => f.Declaration.Variables.First().Identifier.Text).ToList();
        Assert.Contains("InvalidInput", fieldNames);
        Assert.Contains("NotFound", fieldNames);
    }

    [Fact]
    public void GenerateFailureClass_ForDuplicateName_ReportsError()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [Failure("InvalidInput", "first", "1001")]
                [Failure("InvalidInput", "second", "1002")]
                public partial class MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Empty(result.GeneratedTrees);
        Assert.Single(result.Diagnostics, d => d.Id == "NOF001");
        Assert.Contains("MyFailure", result.Diagnostics.First().GetMessage());
        Assert.Contains("InvalidInput", result.Diagnostics.First().GetMessage());
    }

    [Fact]
    public void GenerateFailureClass_ForDuplicateErrorCode_ReportsInfoAndStillGenerates()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [Failure("InvalidInput", "first", "1001")]
                [Failure("InvalidParameter", "second", "1001")]
                public partial class MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Single(result.GeneratedTrees);
        var diag = Assert.Single(result.Diagnostics, d => d.Id == "NOF002");
        Assert.Equal(DiagnosticSeverity.Info, diag.Severity);
        Assert.Contains("MyFailure", diag.GetMessage());
        Assert.Contains("1001", diag.GetMessage());
    }

    [Fact]
    public void GenerateFailureClass_ForRecord_GeneratesRecordSyntax()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [Failure("InvalidInput", "invalid input", "1001")]
                public partial record MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Single(result.GeneratedTrees);

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var recordDecl = root.DescendantNodes().OfType<RecordDeclarationSyntax>().Single();
        Assert.Equal("MyFailure", recordDecl.Identifier.Text);
    }

    [Fact]
    public void GenerateFailureClass_ForAbstractClass_GeneratesAbstractSyntax()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [Failure("InvalidInput", "invalid input", "1001")]
                public abstract partial class MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Single(result.GeneratedTrees);

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        Assert.Contains(classDecl.Modifiers, m => m.Text == "abstract");
    }

    [Fact]
    public void GenerateFailureClass_ForClassesInDifferentNamespaces_GeneratesSeparateFiles()
    {
        const string source = """
            using NOF.Domain;
            namespace Test1
            {
                [Failure("InvalidInput", "invalid input", "1001")]
                public partial class MyFailure
                {
                }
            }
            namespace Test2
            {
                [Failure("NotFound", "not found", "1002")]
                public partial class MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Equal(2, result.GeneratedTrees.Count());

        var fileNames = result.GeneratedTrees.Select(t => t.FilePath).ToList();
        Assert.Contains(fileNames, f => f.Contains("Test1_MyFailure.g.cs"));
        Assert.Contains(fileNames, f => f.Contains("Test2_MyFailure.g.cs"));
    }

    [Fact]
    public void GenerateFailureClass_ForNonPartialClass_SkipsGeneration()
    {
        const string source = """
            using NOF.Domain;
            namespace Test
            {
                [Failure("InvalidInput", "invalid input", "1001")]
                public class MyFailure
                {
                }
            }
            """;

        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        Assert.Empty(result.GeneratedTrees);
        Assert.Empty(result.Diagnostics);
    }
}
