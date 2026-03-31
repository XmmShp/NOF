using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Contract;
using NOF.Hosting.AspNetCore.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointMapperTests
{
    private static readonly Type[] _refs =
    [
        typeof(HttpEndpointAttribute),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>)
    ];

    [Fact]
    public void GenerateMapAllHttpEndpoints_WithMainAndReferencedServices_CombinesAll()
    {
        const string libSource = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace Lib
            {
                public record GetUserRequest(string Id);

                
                public partial interface ILibService : IRpcService
                {
                    [HttpEndpoint(HttpVerb.Get, "/api/user")]
                    Task<Result<string>> GetUserAsync(GetUserRequest request);
                }
            }
            """;

        var libComp = CSharpCompilation.CreateCompilation("Lib", libSource, isDll: true, _refs);
        var libRef = libComp.CreateMetadataReference();

        const string mainSource = """
            using NOF.Contract;
            using System.Threading;
            using System.Threading.Tasks;

            namespace App
            {
                public record CreateUserRequest(string Name);

                
                public partial interface IAppService : IRpcService
                {
                    [HttpEndpoint(HttpVerb.Post, "/api/user")]
                    Task<Result> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
                }
            }
            """;

        var mainComp = CSharpCompilation.CreateCompilation("App", mainSource, isDll: true, libRef);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(mainComp);

        result.GeneratedTrees.Should().ContainSingle();
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();

        code.Should().Contain("app.MapGet(\"/api/user\"");
        code.Should().Contain("app.MapPost(\"/api/user\"");
        code.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromServicesAttribute] Lib.ILibService service");
        code.Should().Contain("[global::Microsoft.AspNetCore.Mvc.FromServicesAttribute] App.IAppService service");
        code.Should().Contain("service.GetUserAsync(request)");
        code.Should().Contain("service.CreateUserAsync(request, cancellationToken)");
        code.Should().NotContain("IRequestDispatcher");
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
    public void GenerateMapAllHttpEndpoints_MethodWithoutHttpEndpoint_DefaultsToPost()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App
            {
                public record InternalRequest(string Data);

                
                public partial interface IMyService : IRpcService
                {
                    Task<Result> InternalAsync(InternalRequest request);
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();
        code.Should().Contain("MapPost");
        code.Should().Contain("\"Internal\"");
    }

    [Fact]
    public void GenerateMapAllHttpEndpoints_RouteAndBodyHybridBinding_Works()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;

            namespace App
            {
                public record UpdateItemRequest(long Id)
                {
                    public string? Value { get; set; }
                    public int? Priority { get; set; }
                }

                
                public partial interface IMyService : IRpcService
                {
                    [HttpEndpoint(HttpVerb.Patch, "/api/items/{id}")]
                    Task<Result> UpdateItemAsync(UpdateItemRequest request);
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new ExposeToHttpEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();

        code.Should().Contain("class __UpdateItemRequest_Body__");
        code.Should().Contain("new App.UpdateItemRequest(id)");
        code.Should().Contain("Value = __body__.Value");
        code.Should().Contain("Priority = __body__.Priority");
        code.Should().Contain("service.UpdateItemAsync(request)");
    }
}

