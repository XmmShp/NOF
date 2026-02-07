using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
                [ExposeToHttpEndpoint(HttpVerb.Get, "/api/user")]
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
                [ExposeToHttpEndpoint(HttpVerb.Post, "/api/user")]
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
            .And.Contain("[AsParameters] Lib.GetUserRequest request")
            .And.Contain("sender.SendAsync(request)");

        postBlock.Should()
            .Contain("app.MapPost(\"/api/user\"")
            .And.Contain("[FromBody] App.CreateUserRequest request")
            .And.Contain("sender.SendAsync(request)");

        bodyText.Should().Contain("return TypedResults.Ok(response);");
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

    [Fact]
    public void GenerateMapAllHttpEndpoints_RecordWithPrimaryCtorAndExtraProps_UsesHybridBinding()
    {
        // record Request(long Id) { public string? Value { get; set; } }
        // Should generate: new Request(id) { Value = __body__.Value }
        const string source = """
            using NOF;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Patch, "/api/items/{id}")]
                public record UpdateItemRequest(long Id) : IRequest
                {
                    public string? Value { get; set; }
                    public int? Priority { get; set; }
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IRequest),
            typeof(ExposeToHttpEndpointAttribute),
            typeof(HttpVerb)
        );
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Should generate a Body DTO with only non-route properties
        generatedCode.Should().Contain("class __UpdateItemRequest_Body__");
        generatedCode.Should().Contain("public string? Value { get; set; }");
        generatedCode.Should().Contain("public int? Priority { get; set; }");

        // Should use hybrid binding: ctor(routeParam) { extraProp = __body__.extraProp }
        generatedCode.Should().Contain("new App.UpdateItemRequest(id)");
        generatedCode.Should().Contain("Value = __body__.Value");
        generatedCode.Should().Contain("Priority = __body__.Priority");

        // Route param should be a lambda parameter
        generatedCode.Should().Contain("long id");
        // Body DTO should be [FromBody]
        generatedCode.Should().Contain("[FromBody] NOF.Generated.__UpdateItemRequest_Body__ __body__");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_RecordWithAllPropsInCtor_UsesCtorOnly()
    {
        // record Request(long NodeId, string FileName, string Content) — all in ctor
        // Should generate: new Request(nodeId, fileName, __body__.Content)
        const string source = """
            using NOF;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/nodes/{nodeId}/files/{fileName}")]
                public record AddFileRequest(long NodeId, string FileName, string Content) : IRequest;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IRequest),
            typeof(ExposeToHttpEndpointAttribute),
            typeof(HttpVerb)
        );
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Body DTO should only contain Content (non-route prop)
        generatedCode.Should().Contain("class __AddFileRequest_Body__");
        generatedCode.Should().Contain("public string Content { get; set; }");
        generatedCode.Should().NotContain("public long NodeId");
        generatedCode.Should().NotContain("public string FileName { get; set; }");

        // Should use ctor-only binding (no object initializer needed)
        generatedCode.Should().Contain("new App.AddFileRequest(nodeId, fileName, __body__.Content);");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_DeleteWithOnlyRouteParams_NoBodyDto()
    {
        // DELETE /api/items/{id} — only route param, no body
        const string source = """
            using NOF;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Delete, "/api/items/{id}")]
                public record DeleteItemRequest(long Id) : IRequest;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IRequest),
            typeof(ExposeToHttpEndpointAttribute),
            typeof(HttpVerb)
        );
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // No Body DTO should be generated
        generatedCode.Should().NotContain("__DeleteItemRequest_Body__");
        // No [FromBody] parameter
        generatedCode.Should().NotContain("[FromBody]");
        // Should construct request from route param only
        generatedCode.Should().Contain("new App.DeleteItemRequest(id)");
        generatedCode.Should().Contain("long id");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_ClassWithParameterlessCtor_UsesObjectInitializer()
    {
        // Plain class with settable properties, no primary ctor
        const string source = """
            using NOF;
            namespace App
            {
                [ExposeToHttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                public class UpdateItemRequest : IRequest
                {
                    public long Id { get; set; }
                    public string Name { get; set; } = default!;
                    public int Count { get; set; }
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IRequest),
            typeof(ExposeToHttpEndpointAttribute),
            typeof(HttpVerb)
        );
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Body DTO should contain only non-route properties
        generatedCode.Should().Contain("class __UpdateItemRequest_Body__");
        generatedCode.Should().Contain("public string Name { get; set; }");
        generatedCode.Should().Contain("public int Count { get; set; }");

        // Should use object initializer (no parameterized ctor)
        generatedCode.Should().Contain("new App.UpdateItemRequest()");
        generatedCode.Should().Contain("Id = id");
        generatedCode.Should().Contain("Name = __body__.Name");
        generatedCode.Should().Contain("Count = __body__.Count");
    }
}