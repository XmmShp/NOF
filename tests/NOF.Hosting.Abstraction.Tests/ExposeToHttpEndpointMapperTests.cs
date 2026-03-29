using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NOF.Contract;
using NOF.Hosting.AspNetCore.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointMapperTests
{
    private static readonly Type[] _refs =
    [        typeof(HttpEndpointAttribute),
        typeof(GenerateServiceAttribute),
        typeof(HttpVerb),
    ];

    [Fact]
    public void GenerateMapAllHttpEndpoints_WithMainAndReferencedEndpoints_CombinesAll()
    {
        const string libSource = """
            using NOF.Contract;
            namespace Lib
            {
                                [HttpEndpoint(HttpVerb.Get, "/api/user")]
                public record GetUserRequest(string Id);
            }
            """;

        var libComp = CSharpCompilation.CreateCompilation("Lib", libSource, isDll: true, _refs);
        var libRef = libComp.CreateMetadataReference();

        const string mainSource = """
            using NOF.Contract;
            namespace App
            {
                                [HttpEndpoint(HttpVerb.Post, "/api/user")]
                public record CreateUserRequest(string Name);

                [GenerateService(Namespaces = new[] { "App", "Lib" })]
                public partial interface IMyService;
            }
            """;

        var mainComp = CSharpCompilation.CreateCompilation("App", mainSource, isDll: true, libRef);

        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(mainComp);
        var trees = result.GeneratedTrees;

        trees.Should().ContainSingle();

        var root = trees.Single().GetRoot();
        var ns = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Should().ContainSingle().Subject;
        ns.Name.ToString().Should().Be("App");

        var method = ns.DescendantNodes().OfType<MethodDeclarationSyntax>().Should().ContainSingle().Subject;
        method.Identifier.Text.Should().Be("MapAllHttpEndpoints");

        var bodyText = method.Body!.ToString();

        var blocks = bodyText.Split([";\r\n\r\n", ";\n\n"], StringSplitOptions.RemoveEmptyEntries);

        var getBlock = blocks.FirstOrDefault(b => b.Contains("MapGet"));
        var postBlock = blocks.FirstOrDefault(b => b.Contains("MapPost"));

        getBlock.Should().NotBeNull();
        postBlock.Should().NotBeNull();

        getBlock.Should()
            .Contain("app.MapGet(\"/api/user\"")
            .And.Contain("[global::Microsoft.AspNetCore.Http.AsParametersAttribute] Lib.GetUserRequest request")
            .And.Contain("dispatcher.DispatchAsync(request)");

        postBlock.Should()
            .Contain("app.MapPost(\"/api/user\"")
            .And.Contain("[global::Microsoft.AspNetCore.Mvc.FromBodyAttribute] App.CreateUserRequest request")
            .And.Contain("dispatcher.DispatchAsync(request)");

        bodyText.Should().Contain("return global::Microsoft.AspNetCore.Http.TypedResults.Ok(response);");

        // Uses NOF.Infrastructure.IRequestDispatcher, not NOF.Application.IRequestSender
        bodyText.Should().Contain("global::NOF.Infrastructure.IRequestDispatcher");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_WhenNoGenerateService_GeneratesNothing()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public record CreateItemRequest(string Name);
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().BeEmpty();
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
    public void GenerateMapAllHttpEndpoints_PublicApiWithoutHttpEndpoint_DefaultsToPost()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                                public record InternalRequest(string Data);

                [GenerateService]
                public partial interface IMyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("MapPost");
        generatedCode.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromBodyAttribute] App.InternalRequest request");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_RespectsNamespaceFilter()
    {
        const string source = """
            using NOF.Contract;
            namespace Other
            {
                                [HttpEndpoint(HttpVerb.Get, "/api/other")]
                public record OtherRequest;
            }
            namespace App
            {
                                [HttpEndpoint(HttpVerb.Post, "/api/items")]
                public record CreateItemRequest(string Name);

                [GenerateService]
                public partial interface IMyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Only App namespace scanned (default = interface namespace)
        generatedCode.Should().Contain("MapPost");
        generatedCode.Should().Contain("CreateItemRequest");
        generatedCode.Should().NotContain("OtherRequest");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_RecordWithPrimaryCtorAndExtraProps_UsesHybridBinding()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                                [HttpEndpoint(HttpVerb.Patch, "/api/items/{id}")]
                public record UpdateItemRequest(long Id)
                {
                    public string? Value { get; set; }
                    public int? Priority { get; set; }
                }

                [GenerateService]
                public partial interface IMyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("class __UpdateItemRequest_Body__");
        generatedCode.Should().Contain("public string? Value { get; set; }");
        generatedCode.Should().Contain("public int? Priority { get; set; }");

        generatedCode.Should().Contain("new App.UpdateItemRequest(id)");
        generatedCode.Should().Contain("Value = __body__.Value");
        generatedCode.Should().Contain("Priority = __body__.Priority");

        generatedCode.Should().Contain("long id");
        generatedCode.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromBodyAttribute] __UpdateItemRequest_Body__ __body__");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_RecordWithAllPropsInCtor_UsesCtorOnly()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                                [HttpEndpoint(HttpVerb.Put, "/api/nodes/{nodeId}/files/{fileName}")]
                public record AddFileRequest(long NodeId, string FileName, string Content);

                [GenerateService]
                public partial interface IMyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("class __AddFileRequest_Body__");
        generatedCode.Should().Contain("public string Content { get; set; }");
        generatedCode.Should().NotContain("public long NodeId");
        generatedCode.Should().NotContain("public string FileName { get; set; }");

        generatedCode.Should().Contain("new App.AddFileRequest(nodeId, fileName, __body__.Content);");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_DeleteWithOnlyRouteParams_NoBodyDto()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                                [HttpEndpoint(HttpVerb.Delete, "/api/items/{id}")]
                public record DeleteItemRequest(long Id);

                [GenerateService]
                public partial interface IMyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().NotContain("__DeleteItemRequest_Body__");
        generatedCode.Should().NotContain("[global::Microsoft.AspNetCore.Mvc.FromBodyAttribute]");
        generatedCode.Should().Contain("new App.DeleteItemRequest(id)");
        generatedCode.Should().Contain("long id");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_ClassWithParameterlessCtor_UsesObjectInitializer()
    {
        const string source = """
            using NOF.Contract;
            namespace App
            {
                                [HttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                public class UpdateItemRequest
                {
                    public long Id { get; set; }
                    public string Name { get; set; } = default!;
                    public int Count { get; set; }
                }

                [GenerateService]
                public partial interface IMyService;
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("class __UpdateItemRequest_Body__");
        generatedCode.Should().Contain("public string Name { get; set; }");
        generatedCode.Should().Contain("public int Count { get; set; }");

        generatedCode.Should().Contain("new App.UpdateItemRequest()");
        generatedCode.Should().Contain("Id = id");
        generatedCode.Should().Contain("Name = __body__.Name");
        generatedCode.Should().Contain("Count = __body__.Count");
    }
}




