using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NOF.Annotation;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting.AspNetCore;

public static partial class NOFHostingAspNetCoreExtensions
{
    private static readonly MethodInfo _createGetHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateGetHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateGetHandlerCore)}' was not found.");

    private static readonly MethodInfo _createBodyHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateBodyHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateBodyHandlerCore)}' was not found.");

    extension(IEndpointRouteBuilder app)
    {
        [RequiresUnreferencedCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
        [RequiresDynamicCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
        public IEndpointRouteBuilder MapHttpEndpoint<TRpcServer>(string? prefix = null)
            where TRpcServer : RpcServer, IRpcServer
        {
            ArgumentNullException.ThrowIfNull(app);

            var serviceType = TRpcServer.ServiceType;
            var handlerMappings = TRpcServer.HandlerMappings;
            foreach (var (operationName, handlerMapping) in handlerMappings)
            {
                var method = serviceType.GetMethod(operationName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    ?? throw new InvalidOperationException($"RPC contract method '{serviceType.FullName}.{operationName}' was not found.");
                var responseType = GetNormalizedResponseType(handlerMapping.ReturnType);
                var endpoints = GetHttpEndpoints(method, operationName);
                foreach (var (verb, route) in endpoints)
                {
                    var fullRoute = BuildRoute(prefix, route);
                    var handler = CreateEndpointHandler(serviceType, handlerMapping.RequestType, operationName, verb);
                    var builder = verb switch
                    {
                        HttpVerb.Get => app.MapGet(fullRoute, handler),
                        HttpVerb.Post => app.MapPost(fullRoute, handler),
                        HttpVerb.Put => app.MapPut(fullRoute, handler),
                        HttpVerb.Delete => app.MapDelete(fullRoute, handler),
                        HttpVerb.Patch => app.MapPatch(fullRoute, handler),
                        _ => throw new InvalidOperationException($"Unsupported HTTP verb '{verb}'.")
                    };

                    ApplyDocumentation(builder, method);
                    builder.Produces(statusCode: 200, responseType: responseType);
                }
            }

            return app;
        }
    }

    [RequiresUnreferencedCode("Endpoint handler creation uses runtime generic method binding.")]
    [RequiresDynamicCode("Endpoint handler creation uses runtime generic method instantiation.")]
    private static Delegate CreateEndpointHandler(Type serviceType, Type requestType, string operationName, HttpVerb verb)
    {
        var templateMethod = verb == HttpVerb.Get ? _createGetHandlerMethod : _createBodyHandlerMethod;
        var genericMethod = templateMethod.MakeGenericMethod(serviceType, requestType);
        return (Delegate)genericMethod.Invoke(null, [operationName])!;
    }

    private static Delegate CreateGetHandlerCore<TService, TRequest>(string operationName)
        where TService : class, IRpcService
    {
        async Task<object?> Handler([AsParameters] TRequest request, [FromServices] IServiceProvider services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(services);
            return await RpcServerInvoker.InvokeAsync<TService>(services, operationName, request!, cancellationToken).ConfigureAwait(false);
        }

        return (Func<TRequest, IServiceProvider, CancellationToken, Task<object?>>)Handler;
    }

    private static Delegate CreateBodyHandlerCore<TService, TRequest>(string operationName)
        where TService : class, IRpcService
    {
        async Task<object?> Handler([FromBody] TRequest request, [FromServices] IServiceProvider services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(services);
            return await RpcServerInvoker.InvokeAsync<TService>(services, operationName, request!, cancellationToken).ConfigureAwait(false);
        }

        return (Func<TRequest, IServiceProvider, CancellationToken, Task<object?>>)Handler;
    }

    [RequiresDynamicCode("Result type normalization may require runtime generic instantiation.")]
    private static Type GetNormalizedResponseType(Type returnType)
    {
        if (returnType == typeof(Empty) || returnType == typeof(Result))
        {
            return typeof(Result);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            return returnType;
        }

        return typeof(Result<>).MakeGenericType(returnType);
    }

    private static IEnumerable<(HttpVerb Verb, string Route)> GetHttpEndpoints(MethodInfo method, string defaultRoute)
    {
        var metadata = method.GetCustomAttributes(inherit: true).OfType<MetadataAttribute>().ToArray();
        var endpoints = new List<(HttpVerb, string)>();

        foreach (var attr in metadata)
        {
            if (!HttpEndpointAttribute.TryParseMetadataKey(attr.Key, out var verb))
            {
                continue;
            }

            var route = string.IsNullOrWhiteSpace(attr.Value) ? defaultRoute : attr.Value;
            if (route.IndexOf('{') >= 0 || route.IndexOf('}') >= 0)
            {
                throw new InvalidOperationException(
                    $"Route parameters are not supported for RPC HTTP endpoints. Method: '{method.DeclaringType?.FullName}.{method.Name}', route: '{route}'.");
            }

            endpoints.Add((verb, route));
        }

        if (endpoints.Count == 0)
        {
            endpoints.Add((HttpVerb.Post, defaultRoute));
        }

        return endpoints;
    }

    private static void ApplyDocumentation(RouteHandlerBuilder builder, MethodInfo method)
    {
        var metadata = method.GetCustomAttributes(inherit: true).OfType<MetadataAttribute>().ToArray();
        var summary = metadata.LastOrDefault(a => string.Equals(a.Key, SummaryAttribute.MetadataKey, StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.WithSummary(summary);
        }

        var description = method.GetCustomAttributes(inherit: true)
            .OfType<DescriptionAttribute>()
            .LastOrDefault()
            ?.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.WithDescription(description);
        }

        var tags = method.GetCustomAttributes(inherit: true)
            .OfType<CategoryAttribute>()
            .Select(a => a.Category)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
        if (tags.Length > 0)
        {
            builder.WithTags(tags);
        }
    }

    private static string BuildRoute(string? prefix, string route)
    {
        prefix ??= string.Empty;

        if (string.IsNullOrEmpty(prefix))
        {
            return NormalizeRoute(route);
        }

        if (string.IsNullOrEmpty(route))
        {
            return NormalizeRoute(prefix);
        }

        var normalizedPrefix = NormalizeRoute(prefix).TrimEnd('/');
        var normalizedRoute = NormalizeRoute(route);
        return normalizedPrefix + normalizedRoute;
    }

    private static string NormalizeRoute(string route)
    {
        route = route.TrimEnd('/');
        if (string.IsNullOrEmpty(route))
        {
            return "/";
        }

        return route.StartsWith("/", StringComparison.Ordinal) ? route : "/" + route;
    }
}
