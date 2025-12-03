using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NOF.Contract.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class QueryParameterGeneratorTests
{
    [Fact]
    public void GenerateQueryParameterExtensions_ForClassWithProperties_GeneratesCorrectExtensionMethod()
    {
        const string source = """
        using System;
        using NOF;

        namespace TestNamespace
        {
            [QueryParameter]
            public class TestClass
            {
                public string? Name { get; set; }
                public string Class { get; set; }
                public int? Age { get; set; }
                public DateTime BirthDate { get; set; }
            }
        }
        """;

        var runResult = new QueryParameterGenerator().GetResult(source, typeof(QueryParameterAttribute));
        runResult.GeneratedTrees.Should().ContainSingle();

        var tree = runResult.GeneratedTrees[0];
        var root = tree.GetRoot();

        // 1. 应该在 NOF 命名空间中
        var namespaceDecl = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>()
            .Should().ContainSingle("应生成一个命名空间")
            .Subject;
        namespaceDecl.Name.ToString().Should().Be("NOF");

        // 2. 应该有一个静态类 __QueryParameterExtensions__
        var classDecl = namespaceDecl.Members.OfType<ClassDeclarationSyntax>()
            .Should().ContainSingle("应生成一个扩展类")
            .Subject;
        classDecl.Identifier.Text.Should().Be("__QueryParameterExtensions__");
        classDecl.Modifiers.Should().Contain(token => token.IsKind(SyntaxKind.StaticKeyword));

        // 3. 应该有一个扩展方法 ToQueryString(this TestNamespace.TestClass source)
        var method = classDecl.Members.OfType<MethodDeclarationSyntax>()
            .Should().ContainSingle("应生成一个扩展方法")
            .Subject;
        method.Identifier.Text.Should().Be("ToQueryString");
        method.Modifiers.Should().Contain(token => token.IsKind(SyntaxKind.PublicKeyword))
            .And.Contain(token => token.IsKind(SyntaxKind.StaticKeyword));

        // 参数检查
        method.ParameterList.Parameters.Should().HaveCount(1);
        var param = method.ParameterList.Parameters[0];
        param.Modifiers.Should().Contain(token => token.IsKind(SyntaxKind.ThisKeyword));
        param.Type?.ToString().Should().Be("TestNamespace.TestClass");
        param.Identifier.Text.Should().Be("source");

        // 返回类型
        method.ReturnType.ToString().Should().Be("string");

        // 4. 方法体中应包含对每个属性的赋值：queryParams["PropertyName"] = ...
        var body = method.Body;
        body.Should().NotBeNull();

        var assignments = body.DescendantNodes().OfType<ExpressionStatementSyntax>()
            .Select(s => s.Expression)
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => a.Left is ElementAccessExpressionSyntax)
            .ToList();

        // 应有 4 个属性赋值
        assignments.Should().HaveCount(4);

        var assignedKeys = assignments
            .Select(a => ((LiteralExpressionSyntax)((ElementAccessExpressionSyntax)a.Left).ArgumentList.Arguments[0].Expression))
            .Select(lit => lit.Token.ValueText)
            .ToHashSet();

        assignedKeys.Should().BeEquivalentTo("Name", "Class", "Age", "BirthDate");

        // 5. 确保使用了 Uri.EscapeDataString
        var escapeCalls = body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "EscapeDataString" })
            .ToList();

        escapeCalls.Should().HaveCount(2); // 一个用于 key，一个用于 value（在循环内）
    }

    [Fact]
    public void GenerateQueryParameterExtensions_ForMultipleClasses_GeneratesAllExtensionMethods()
    {
        const string source = """
                              using System;
                              using NOF;

                              namespace TestNamespace
                              {
                                  [QueryParameter]
                                  public class UserQuery
                                  {
                                      public string? Name { get; set; }
                                      public string? Email { get; set; }
                                  }

                                  [QueryParameter]
                                  public class ProductQuery
                                  {
                                      public string? Category { get; set; }
                                      public decimal? MinPrice { get; set; }
                                      public decimal? MaxPrice { get; set; }
                                  }
                              }
                              """;

        var runResult = new QueryParameterGenerator().GetResult(source, typeof(QueryParameterAttribute));
        runResult.GeneratedTrees.Should().ContainSingle();

        var tree = runResult.GeneratedTrees[0];
        var root = tree.GetRoot();

        // 1. 命名空间是 NOF
        var namespaceDecl = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>()
            .Should().ContainSingle()
            .Subject;
        namespaceDecl.Name.ToString().Should().Be("NOF");

        // 2. 一个静态类
        var classDecl = namespaceDecl.Members.OfType<ClassDeclarationSyntax>()
            .Should().ContainSingle()
            .Subject;
        classDecl.Identifier.Text.Should().Be("__QueryParameterExtensions__");

        // 3. 两个扩展方法
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>().ToList();
        methods.Should().HaveCount(2);

        var methodNames = methods.Select(m => m.Identifier.Text).ToList();
        methodNames.Should().BeEquivalentTo("ToQueryString", "ToQueryString"); // 同名重载

        // 检查参数类型区分
        var paramTypes = methods.Select(m => m.ParameterList.Parameters[0].Type?.ToString()).ToHashSet();
        paramTypes.Should().BeEquivalentTo("TestNamespace.UserQuery", "TestNamespace.ProductQuery");

        // 4. 每个方法体都有 queryParams 赋值
        foreach (var method in methods)
        {
            method.Body.Should().NotBeNull();
            var assignments = method.Body!.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Where(a => a.Left is ElementAccessExpressionSyntax)
                .ToList();

            if (method.ParameterList.Parameters[0].Type?.ToString() == "TestNamespace.UserQuery")
            {
                assignments.Should().HaveCount(2);
            }
            else if (method.ParameterList.Parameters[0].Type?.ToString() == "TestNamespace.ProductQuery")
            {
                assignments.Should().HaveCount(3);
            }
        }
    }

    [Fact]
    public void GenerateQueryParameterExtensions_WithDateTimeTypes_UsesCorrectToStringFormat()
    {
        const string source = """
                          using System;
                          using NOF;

                          namespace TestNamespace
                          {
                              [QueryParameter]
                              public class TimeQuery
                              {
                                  public DateTime CreatedAt { get; set; }
                                  public DateTime? UpdatedAt { get; set; }
                                  public DateTimeOffset EventTime { get; set; }
                                  public DateTimeOffset? ScheduledTime { get; set; }
                                  public DateOnly BirthDate { get; set; }
                                  public TimeOnly StartTime { get; set; }
                              }
                          }
                          """;

        var runResult = new QueryParameterGenerator().GetResult(source, typeof(QueryParameterAttribute));
        runResult.GeneratedTrees.Should().ContainSingle();

        var tree = runResult.GeneratedTrees[0];
        var root = tree.GetRoot();

        // 找到生成的扩展方法（针对 TimeQuery）
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.ParameterList.Parameters.Count == 1 &&
                                 m.ParameterList.Parameters[0].Type?.ToString() == "TestNamespace.TimeQuery");

        method.Should().NotBeNull("应生成 TimeQuery 的 ToQueryString 扩展方法");

        // 提取所有赋值语句：queryParams["xxx"] = ...
        var assignments = method!.Body!
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => a.Left is ElementAccessExpressionSyntax access &&
                        access.Expression.ToString() == "queryParams")
            .ToList();

        assignments.Should().HaveCount(6);

        // 构造期望的属性名 -> 期望的 ToString 调用模式
        var expectedFormats = new Dictionary<string, string>
        {
            ["CreatedAt"] = ".ToString(\"O\")",
            ["UpdatedAt"] = "?.ToString(\"O\")",
            ["EventTime"] = ".ToString(\"O\")",
            ["ScheduledTime"] = "?.ToString(\"O\")",
            ["BirthDate"] = ".ToString(\"yyyy-MM-dd\")",
            ["StartTime"] = ".ToString(\"HH:mm:ss.FFFFFFF\")"
        };

        foreach (var (propName, expectedSuffix) in expectedFormats)
        {
            var assignment = assignments
                .FirstOrDefault(a => a.Left is ElementAccessExpressionSyntax e &&
                                     e.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit &&
                                     lit.Token.ValueText == propName);

            assignment.Should().NotBeNull($"应找到属性 {propName} 的赋值语句");

            var rightExpr = assignment!.Right.ToString();
            rightExpr.Should().EndWith(expectedSuffix,
                $"属性 {propName} 应使用正确的格式化 ToString，实际为: {rightExpr}");
        }
    }
}
