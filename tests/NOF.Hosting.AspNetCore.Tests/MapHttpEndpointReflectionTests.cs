using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NOF.Contract;
using NOF.Hosting.AspNetCore;
using System.Reflection;
using Xunit;

namespace NOF.Hosting.AspNetCore.Tests;

public class MapHttpEndpointReflectionTests
{
    private static readonly MethodInfo CreateEndpointHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod("CreateEndpointHandler", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Method 'CreateEndpointHandler' not found.");

    private static readonly MethodInfo GetHttpEndpointsMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod("GetHttpEndpoints", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Method 'GetHttpEndpoints' not found.");

    private static readonly MethodInfo GetNormalizedResponseTypeMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod("GetNormalizedResponseType", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Method 'GetNormalizedResponseType' not found.");

    public record GetUserRequest(string Name);
    public record CreateUserRequest(string Name);
    public record UserDto(string Name);

    private interface IAppService : IRpcService
    {
        [HttpEndpoint(HttpVerb.Get, "/rpc/GetUser")]
        UserDto GetUser(GetUserRequest request);

        [HttpEndpoint(HttpVerb.Post, "/rpc/CreateUser")]
        Empty CreateUser(CreateUserRequest request);

        Result Ping(GetUserRequest request);

        [HttpEndpoint(HttpVerb.Get, "/rpc/{id}")]
        Result InvalidRoute(GetUserRequest request);
    }

    [Fact]
    public void GetNormalizedResponseType_WhenCalled_MapsToRpcResultConvention()
    {
        Assert.Equal(typeof(Result), GetNormalizedResponseType(typeof(Result)));
        Assert.Equal(typeof(Result), GetNormalizedResponseType(typeof(Empty)));
        Assert.Equal(typeof(Result<UserDto>), GetNormalizedResponseType(typeof(Result<UserDto>)));
        Assert.Equal(typeof(Result<UserDto>), GetNormalizedResponseType(typeof(UserDto)));
    }

    [Fact]
    public void CreateEndpointHandler_WhenGet_UsesAsParametersBinding()
    {
        var handler = CreateHandler(nameof(IAppService.GetUser), HttpVerb.Get, typeof(GetUserRequest));
        var requestParameter = handler.Method.GetParameters()[0];

        Assert.NotNull(requestParameter.GetCustomAttribute<AsParametersAttribute>());
        Assert.Null(requestParameter.GetCustomAttribute<FromBodyAttribute>());
    }

    [Fact]
    public void CreateEndpointHandler_WhenPost_UsesFromBodyBinding()
    {
        var handler = CreateHandler(nameof(IAppService.CreateUser), HttpVerb.Post, typeof(CreateUserRequest));
        var requestParameter = handler.Method.GetParameters()[0];

        Assert.NotNull(requestParameter.GetCustomAttribute<FromBodyAttribute>());
        Assert.Null(requestParameter.GetCustomAttribute<AsParametersAttribute>());
    }

    [Fact]
    public void GetHttpEndpoints_WhenNoHttpEndpointMetadata_UsesPostAndOperationNameRoute()
    {
        var method = typeof(IAppService).GetMethod(nameof(IAppService.Ping))
            ?? throw new InvalidOperationException("Method 'Ping' not found.");
        var endpoints = GetHttpEndpoints(method, nameof(IAppService.Ping));

        Assert.Single(endpoints);
        Assert.Equal(HttpVerb.Post, endpoints[0].Verb);
        Assert.Equal(nameof(IAppService.Ping), endpoints[0].Route);
    }

    [Fact]
    public void GetHttpEndpoints_WhenRouteContainsParameters_Throws()
    {
        var method = typeof(IAppService).GetMethod(nameof(IAppService.InvalidRoute))
            ?? throw new InvalidOperationException("Method 'InvalidRoute' not found.");

        var exception = Assert.Throws<TargetInvocationException>(() => GetHttpEndpoints(method, nameof(IAppService.InvalidRoute)));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("Route parameters are not supported", exception.InnerException!.Message);
    }

    private static Type GetNormalizedResponseType(Type returnType)
        => (Type)GetNormalizedResponseTypeMethod.Invoke(null, [returnType])!;

    private static Delegate CreateHandler(string operationName, HttpVerb verb, Type requestType)
        => (Delegate)CreateEndpointHandlerMethod.Invoke(null, [typeof(IAppService), requestType, operationName, verb])!;

    private static (HttpVerb Verb, string Route)[] GetHttpEndpoints(MethodInfo method, string defaultRoute)
        => ((IEnumerable<(HttpVerb Verb, string Route)>)GetHttpEndpointsMethod.Invoke(null, [method, defaultRoute])!).ToArray();
}
