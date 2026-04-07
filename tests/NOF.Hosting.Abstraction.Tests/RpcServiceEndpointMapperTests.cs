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
        typeof(Hosting.AspNetCore.NOFHostingAspNetCoreExtensions),
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

        Assert.Single(result.GeneratedTrees);
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("InterceptsLocation", code);
        Assert.Contains("app.MapGet(BuildRoute(prefix, \"/api/user\")", code);
        Assert.Contains("app.MapPost(BuildRoute(prefix, \"/api/user\")", code);
        Assert.Contains("[global::Microsoft.AspNetCore.Mvc.FromServicesAttribute] Lib.ILibService service", code);
        Assert.Contains("[global::Microsoft.AspNetCore.Mvc.FromServicesAttribute] App.IAppService service", code);
        Assert.Contains("GetUserAsync(request)", code);
        Assert.Contains("CreateUserAsync(request, cancellationToken)", code);
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
        Assert.Empty(

        result.GeneratedTrees);
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

        Assert.Single(result.GeneratedTrees);
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();
        Assert.Contains("MapPost", code);
        Assert.Contains("BuildRoute(prefix, \"Internal\")", code);
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

        Assert.Single(result.GeneratedTrees);
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("class __App_IMyService_UpdateItemRequest_Body__", code);
        Assert.Contains("new App.UpdateItemRequest(id)", code);
        Assert.Contains("Value = __body__.Value", code);
        Assert.Contains("Priority = __body__.Priority", code);
        Assert.Contains("UpdateItemAsync(request)", code);
    }
}


