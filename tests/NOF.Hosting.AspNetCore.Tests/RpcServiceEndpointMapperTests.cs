using Microsoft.CodeAnalysis.CSharp;
using NOF.Annotation;
using NOF.Contract;
using NOF.Hosting.AspNetCore.SourceGenerator;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcHttpEndpointRegistryGeneratorTests
{
    private static readonly Type[] _refs =
    [
        typeof(HttpEndpointAttribute),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>),
        typeof(AssemblyInitializeAttribute),
        typeof(Hosting.AspNetCore.RpcHttpEndpointHandlerRegistration),
        typeof(Hosting.AspNetCore.RpcHttpEndpointHandlerInfos),
        typeof(Hosting.AspNetCore.NOFHostingAspNetCoreExtensions),
        typeof(Microsoft.AspNetCore.Mvc.FromBodyAttribute),
        typeof(Microsoft.AspNetCore.Mvc.FromServicesAttribute),
        typeof(Application.RpcServer<>),
        typeof(Infrastructure.RpcServerInvoker),
        typeof(Microsoft.AspNetCore.Builder.WebApplication)
    ];

    [Fact]
    public void GenerateMapHttpEndpoint_WhenCalled_GeneratesInitializerAndRegistrations()
    {
        const string mainSource = """
            using System;
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Builder;
            using NOF.Application;
            using NOF.Contract;
            using NOF.Hosting.AspNetCore;

            namespace App
            {
                public record CreateUserRequest(string Name);

                
                public partial interface IAppService : IRpcService
                {
                    [HttpEndpoint(HttpVerb.Post, "/rpc/CreateUser")]
                    Result CreateUser(CreateUserRequest request);
                }

                public sealed class AppServer : RpcServer<IAppService>
                {
                    protected override IReadOnlyDictionary<string, Type> GetHandlerMappings()
                        => new Dictionary<string, Type>();
                }

                public static class Startup
                {
                    public static void Configure(WebApplication app)
                    {
                        app.MapHttpEndpoint<AppServer>();
                    }
                }
            }
            """;

        var mainComp = CSharpCompilation.CreateCompilation("App", mainSource, isDll: true, _refs);
        var result = new RpcHttpEndpointRegistryGenerator().GetResult(mainComp);

        Assert.Single(result.GeneratedTrees);
        var code = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("AssemblyInitializeAttribute<global::App.__AppRpcHttpEndpointAssemblyInitializer>", code);
        Assert.Contains("Registry.RpcHttpEndpointHandlerRegistrations.Add(new global::NOF.Hosting.AspNetCore.RpcHttpEndpointHandlerRegistration(typeof(global::App.IAppService), \"CreateUser\"", code);
        Assert.Contains("RpcServerInvoker.InvokeAsync<global::App.IAppService>(services, \"CreateUser\", request, cancellationToken)", code);
    }

    [Fact]
    public void GenerateMapHttpEndpoint_WhenNoCall_GeneratesNothing()
    {
        const string source = """
            using NOF.Contract;
            
            [assembly: System.CLSCompliant(true)]

            namespace App
            {
                public record CreateItemRequest(string Name);

                
                public partial interface IItemService : IRpcService
                {
                    [HttpEndpoint(HttpVerb.Post, "/api/items")]
                    Result Create(CreateItemRequest request);
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true, _refs);
        var result = new RpcHttpEndpointRegistryGenerator().GetResult(comp);
        Assert.Empty(result.GeneratedTrees);
    }
}
