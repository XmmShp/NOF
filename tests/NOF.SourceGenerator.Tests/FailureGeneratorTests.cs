using FluentAssertions;
using NOF.Domain.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class FailureGeneratorTests
{
    [Fact]
    public void GenerateFailureClass_ForSingleClass_GeneratesCorrectCode()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                [Failure("NotFound", "资源未找到", 1002)]
                public partial class MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().ContainSingle();

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var ns = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax>().Single();
        ns.Name.ToString().Should().Be("Test");

        var classDecl = ns.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single();
        classDecl.Identifier.Text.Should().Be("MyFailure");

        var fields = classDecl.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>().ToList();
        fields.Should().HaveCount(2);

        var invalidInputField = fields.First(f => f.Declaration.Variables.First().Identifier.Text == "InvalidInput");
        invalidInputField.ToString().Should().Contain("public static readonly NOF.Failure InvalidInput = new(\"输入无效\", 1001);");

        var notFoundField = fields.First(f => f.Declaration.Variables.First().Identifier.Text == "NotFound");
        notFoundField.ToString().Should().Contain("public static readonly NOF.Failure NotFound = new(\"资源未找到\", 1002);");
    }

    [Fact]
    public void GenerateFailureClass_ForPartialClassesInSameNamespace_MergesAndGeneratesSingleFile()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                public partial class MyFailure
                {
                }

                [Failure("NotFound", "资源未找到", 1002)]
                public partial class MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().ContainSingle();

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single();

        var fields = classDecl.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>().ToList();
        fields.Should().HaveCount(2);

        var fieldNames = fields.Select(f => f.Declaration.Variables.First().Identifier.Text).ToList();
        fieldNames.Should().Contain("InvalidInput");
        fieldNames.Should().Contain("NotFound");
    }

    [Fact]
    public void GenerateFailureClass_ForDuplicateName_ReportsError()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                [Failure("InvalidInput", "输入参数无效", 1002)]
                public partial class MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "NOF001");
        result.Diagnostics.First().GetMessage().Should().Contain("MyFailure").And.Contain("InvalidInput");
    }

    [Fact]
    public void GenerateFailureClass_ForDuplicateErrorCode_ReportsError()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                [Failure("InvalidParameter", "输入参数无效", 1001)]
                public partial class MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle(d => d.Id == "NOF002");
        result.Diagnostics.First().GetMessage().Should().Contain("MyFailure").And.Contain("1001");
    }

    [Fact]
    public void GenerateFailureClass_ForRecord_GeneratesRecordSyntax()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                public partial record MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().ContainSingle();

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var recordDecl = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax>().Single();
        recordDecl.Identifier.Text.Should().Be("MyFailure");
    }

    [Fact]
    public void GenerateFailureClass_ForAbstractClass_GeneratesAbstractSyntax()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                public abstract partial class MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().ContainSingle();

        var tree = result.GeneratedTrees.Single();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single();
        classDecl.Modifiers.Should().Contain(m => m.Text == "abstract");
    }

    [Fact]
    public void GenerateFailureClass_ForClassesInDifferentNamespaces_GeneratesSeparateFiles()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test1
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                public partial class MyFailure
                {
                }
            }
            namespace Test2
            {
                [Failure("NotFound", "资源未找到", 1002)]
                public partial class MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().HaveCount(2);

        var fileNames = result.GeneratedTrees.Select(t => t.FilePath).ToList();
        fileNames.Should().Contain(f => f.Contains("Test1_MyFailure.g.cs"));
        fileNames.Should().Contain(f => f.Contains("Test2_MyFailure.g.cs"));
    }

    [Fact]
    public void GenerateFailureClass_ForNonPartialClass_SkipsGeneration()
    {
        // Arrange
        const string source = """
            using NOF;
            namespace Test
            {
                [Failure("InvalidInput", "输入无效", 1001)]
                public class MyFailure
                {
                }
            }
            """;

        // Act
        var result = new FailureGenerator().GetResult(source, typeof(FailureAttribute));

        // Assert
        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }
}
