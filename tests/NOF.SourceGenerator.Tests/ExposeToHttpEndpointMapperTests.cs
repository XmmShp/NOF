using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NOF.Infrastructure.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointMapperTests
{
    [Fact]
    public void GenerateMapAllHttpEndpoints_WithMainAndReferencedEndpoints_CombinesAll()
    {
        // --- 引用类库：包含一个 GET 请求（IRequest<Guid>） + AllowAnonymous ---
        const string libSource = """
            using NOF;
            namespace Lib
            {
                [ExposeToHttpEndpoint(HttpVerb.Get, "/api/user", AllowAnonymous = true)]
                public record GetUserRequest(string Id) : IRequest<System.Guid>;
            }
            """;

        var libComp = CSharpCompilation.CreateCompilation(
            "Lib",
            libSource,
            isDll: true,
            typeof(IRequest<>),
            typeof(ExposeToHttpEndpointAttribute),
            typeof(HttpVerb)
        );
        var libRef = libComp.CreateMetadataReference();

        // --- 主项目：包含一个 POST 请求（IRequest） + Permission ---
        const string mainSource = """
            using NOF;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/user", Permission = "User.Create")]
                public record CreateUserRequest(string Name) : IRequest;
            }
            """;

        var mainComp = CSharpCompilation.CreateCompilation(
            "App",
            mainSource,
            isDll: true,
            libRef
        );

        // --- 执行生成器 ---
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(mainComp);
        var trees = result.GeneratedTrees;

        // 应该只生成一个文件
        trees.Should().ContainSingle();

        // 解析生成的语法树
        var root = trees.Single().GetRoot();
        var ns = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Should().ContainSingle().Subject;
        ns.Name.ToString().Should().Be("NOF.Generated");

        var method = ns.DescendantNodes().OfType<MethodDeclarationSyntax>().Should().ContainSingle().Subject;
        method.Identifier.Text.Should().Be("MapAllHttpEndpoints");

        var bodyText = method.Body!.ToString();

        // 🔍 分割两个端点（按空行或分号+换行）
        var blocks = bodyText.Split([";\r\n\r\n", ";\n\n"], StringSplitOptions.RemoveEmptyEntries);

        // 找到 GET 块和 POST 块
        var getBlock = blocks.FirstOrDefault(b => b.Contains("MapGet"));
        var postBlock = blocks.FirstOrDefault(b => b.Contains("MapPost"));

        getBlock.Should().NotBeNull();
        postBlock.Should().NotBeNull();

        getBlock.Should()
            .Contain("app.MapGet(\"/api/user\"")
            .And.Contain("[FromQuery] Lib.GetUserRequest request")
            .And.Contain("mediator.SendRequest(request)")
            .And.Contain(".AllowAnonymous()")
            .And.NotContain("RequirePermission");

        postBlock.Should()
            .Contain("app.MapPost(\"/api/user\"")
            .And.Contain("[FromBody] App.CreateUserRequest request")
            .And.Contain("mediator.SendRequest(request)")
            .And.Contain(".RequirePermission(\"User.Create\")")
            .And.NotContain("AllowAnonymous");

        bodyText.Should().Contain("return Results.Ok(response);");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_WhenNoEndpointsExist_GeneratesNothing()
    {
        const string source = """
            namespace App
            {
                public class PlainClass { }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().BeEmpty();
    }
}