using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NOF.Contract;
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

    public record GetUserRequest(string Name);
    public record CreateUserRequest(string Name);
    public record DeleteUserRequest(string Name);
    public record StreamUsersRequest(string Name);
    public record UserDto(string Name);

    private interface IAppService : IRpcService
    {
        [HttpEndpoint(HttpVerb.Get, "/rpc/GetUser")]
        Result<UserDto> GetUser(GetUserRequest request);

        [HttpEndpoint(HttpVerb.Post, "/rpc/CreateUser")]
        Result CreateUser(CreateUserRequest request);

        [HttpEndpoint(HttpVerb.Delete, "/rpc/DeleteUser")]
        Result DeleteUser(DeleteUserRequest request);

        [HttpEndpoint(HttpVerb.Get, "/rpc/StreamUsers")]
        StreamingResult<UserDto> StreamUsers(StreamUsersRequest request);

        Result Ping(GetUserRequest request);

        [HttpEndpoint(HttpVerb.Get, "/rpc/{id}")]
        Result InvalidRoute(GetUserRequest request);
    }

    [Fact]
    public void CreateEndpointHandler_WhenGet_UsesAsParametersBinding()
    {
        var handler = CreateHandler(nameof(IAppService.GetUser), HttpVerb.Get, typeof(GetUserRequest), typeof(Result<UserDto>));
        var requestParameter = handler.Method.GetParameters()[0];

        Assert.Equal(typeof(Task<Microsoft.AspNetCore.Http.IResult>), handler.Method.ReturnType);
        Assert.NotNull(requestParameter.GetCustomAttribute<AsParametersAttribute>());
        Assert.Null(requestParameter.GetCustomAttribute<FromBodyAttribute>());
    }

    [Fact]
    public void CreateEndpointHandler_WhenPost_UsesFromBodyBinding()
    {
        var handler = CreateHandler(nameof(IAppService.CreateUser), HttpVerb.Post, typeof(CreateUserRequest), typeof(Result));
        var requestParameter = handler.Method.GetParameters()[0];

        Assert.Equal(typeof(Task<Microsoft.AspNetCore.Http.IResult>), handler.Method.ReturnType);
        Assert.NotNull(requestParameter.GetCustomAttribute<FromBodyAttribute>());
        Assert.Null(requestParameter.GetCustomAttribute<AsParametersAttribute>());
    }

    [Fact]
    public void CreateEndpointHandler_WhenStreamingGet_ReturnsIResult()
    {
        var handler = CreateHandler(nameof(IAppService.StreamUsers), HttpVerb.Get, typeof(StreamUsersRequest), typeof(StreamingResult<UserDto>));

        Assert.Equal(typeof(Task<Microsoft.AspNetCore.Http.IResult>), handler.Method.ReturnType);
        var requestParameter = handler.Method.GetParameters()[0];
        Assert.NotNull(requestParameter.GetCustomAttribute<AsParametersAttribute>());
    }

    [Fact]
    public void CreateEndpointHandler_WhenDelete_UsesAsParametersBinding()
    {
        var handler = CreateHandler(nameof(IAppService.DeleteUser), HttpVerb.Delete, typeof(DeleteUserRequest), typeof(Result));
        var requestParameter = handler.Method.GetParameters()[0];

        Assert.NotNull(requestParameter.GetCustomAttribute<AsParametersAttribute>());
        Assert.Null(requestParameter.GetCustomAttribute<FromBodyAttribute>());
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
        Assert.Contains("Route parameters are not supported", exception.InnerException.Message);
    }

    private static Delegate CreateHandler(string operationName, HttpVerb verb, Type requestType, Type returnType)
        => (Delegate)CreateEndpointHandlerMethod.Invoke(null, [typeof(IAppService), requestType, operationName, verb, returnType])!;

    private static (HttpVerb Verb, string Route)[] GetHttpEndpoints(MethodInfo method, string defaultRoute)
        => ((IEnumerable<(HttpVerb Verb, string Route)>)GetHttpEndpointsMethod.Invoke(null, [method, defaultRoute])!).ToArray();
}
