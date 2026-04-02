using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Contract;
using NOF.Hosting.AspNetCore.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcServiceEndpointMapperTests
{
    private static readonly Type[] _refs =
    [
        typeof(HttpEndpointAttribute),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>),
        typeof(NOF.Hosting.AspNetCore.NOFHostingAspNetCoreExtensions),
        typeof(Microsoft.AspNetCore.Builder.WebApplication)
    ];

    [Fact]
    public void GenerateMapServiceToHttpEndpoints_WithMainAndReferencedServices_CombinesAll()
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
            using NOF.Hosting.AspNetCore;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;

            namespace App
            {
                public record CreateUserRequest(string Name);

                
                public partial interface IAppService : IRpcService
                {
                    [HttpEndpoint(HttpVerb.Post, "/api/user")]
                    Task<Result> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
                }

                public static class Program
                {
                    public static void Configure(WebApplication app)
                    {
                        app.MapServiceToHttpEndpoints<Lib.ILibService>();
                        app.MapServiceToHttpEndpoints<IAppService>("/v1");
                    }
                }
            }
            """;

        var mainRefs = _refs.Select(static type => type.ToMetadataReference())
            .Cast<MetadataReference>()
            .Append(libRef)
            .ToArray();
        var mainComp = CSharpCompilation.CreateCompilation("App", mainSource, isDll: true, mainRefs);
        var result = new RpcServiceEndpointMapperGenerator().GetResult(mainComp);

        result.GeneratedTrees.Should().ContainSingle();
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();

        code.Should().Contain("InterceptsLocation");
        code.Should().Contain("app.MapGet(BuildRoute(prefix, \"/api/user\")");
        code.Should().Contain("app.MapPost(BuildRoute(prefix, \"/api/user\")");
        code.Should().Contain("HttpContext httpContext");
        code.Should().Contain("sp.GetRequiredService<Lib.ILibService>()");
        code.Should().Contain("sp.GetRequiredService<App.IAppService>()");
        code.Should().Contain("GetUserAsync(request)");
        code.Should().Contain("CreateUserAsync(request, ct2)");
    }

    [Fact]
    public void GenerateMapServiceToHttpEndpoints_WhenNoInvocation_GeneratesNothing()
    {
        const string source = """
            using NOF.Contract;
            using System.Threading.Tasks;
            namespace App
            {
                public record CreateItemRequest(string Name);

                
                public partial interface IItemService : IRpcService
                {
                    [HttpEndpoint(HttpVerb.Post, "/api/items")]
                    Task<Result> CreateAsync(CreateItemRequest request);
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new RpcServiceEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void GenerateMapServiceToHttpEndpoints_MethodWithoutHttpEndpoint_DefaultsToPost()
    {
        const string source = """
            using Microsoft.AspNetCore.Builder;
            using NOF.Contract;
            using NOF.Hosting.AspNetCore;
            using System.Threading.Tasks;

            namespace App
            {
                public record InternalRequest(string Data);

                
                public partial interface IMyService : IRpcService
                {
                    Task<Result> InternalAsync(InternalRequest request);
                }

                public static class Program
                {
                    public static void Configure(WebApplication app)
                    {
                        app.MapServiceToHttpEndpoints<IMyService>();
                    }
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new RpcServiceEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();
        code.Should().Contain("MapPost");
        code.Should().Contain("BuildRoute(prefix, \"Internal\")");
    }

    [Fact]
    public void GenerateMapServiceToHttpEndpoints_RouteAndBodyHybridBinding_Works()
    {
        const string source = """
            using Microsoft.AspNetCore.Builder;
            using NOF.Contract;
            using NOF.Hosting.AspNetCore;
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

                public static class Program
                {
                    public static void Configure(WebApplication app)
                    {
                        app.MapServiceToHttpEndpoints<IMyService>();
                    }
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new RpcServiceEndpointMapperGenerator().GetResult(comp);

        result.GeneratedTrees.Should().ContainSingle();
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();

        code.Should().Contain("class __App_IMyService_UpdateItemRequest_Body__");
        code.Should().Contain("new App.UpdateItemRequest(id)");
        code.Should().Contain("Value = __body__.Value");
        code.Should().Contain("Priority = __body__.Priority");
        code.Should().Contain("UpdateItemAsync(request)");
    }
}
