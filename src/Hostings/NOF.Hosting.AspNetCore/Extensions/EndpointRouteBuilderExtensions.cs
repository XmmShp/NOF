using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.AspNetCore.Routing;

[RequiresUnreferencedCode("Endpoint mapping and response writing use reflection and runtime JSON serialization.")]
[RequiresDynamicCode("Endpoint mapping and response writing use reflection and runtime JSON serialization.")]
public static partial class NOFHostingAspNetCoreExtensions
{
    private static readonly MethodInfo _createQueryHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateQueryHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateQueryHandlerCore)}' was not found.");

    private static readonly MethodInfo _createBodyHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateBodyHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateBodyHandlerCore)}' was not found.");

    private static readonly MethodInfo _createStreamQueryHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateStreamQueryHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateStreamQueryHandlerCore)}' was not found.");

    private static readonly MethodInfo _createStreamBodyHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateStreamBodyHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateStreamBodyHandlerCore)}' was not found.");

    private static readonly MethodInfo _createHeaderAwareQueryHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateHeaderAwareQueryHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateHeaderAwareQueryHandlerCore)}' was not found.");

    private static readonly MethodInfo _createHeaderAwareStreamQueryHandlerMethod = typeof(NOFHostingAspNetCoreExtensions)
        .GetMethod(nameof(CreateHeaderAwareStreamQueryHandlerCore), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method '{nameof(CreateHeaderAwareStreamQueryHandlerCore)}' was not found.");

    extension(IEndpointRouteBuilder app)
    {
        [RequiresUnreferencedCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
        [RequiresDynamicCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
        public IEndpointRouteBuilder MapHttpEndpoint<TRpcServer>(string? prefix = null)
            where TRpcServer : RpcServer, IRpcServer
        {
            ArgumentNullException.ThrowIfNull(app);

            return MapHttpEndpointCore(
                app,
                CreateHttpEndpointMappingKey(typeof(TRpcServer), prefix),
                TRpcServer.ServiceType,
                TRpcServer.HandlerMappings,
                prefix);
        }
    }

    [RequiresUnreferencedCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
    [RequiresDynamicCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
    internal static IEndpointRouteBuilder MapHttpEndpoint(
        IEndpointRouteBuilder app,
        Type rpcServerType,
        string? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(rpcServerType);

        if (!typeof(RpcServer).IsAssignableFrom(rpcServerType)
            || !typeof(IRpcServer).IsAssignableFrom(rpcServerType))
        {
            throw new InvalidOperationException($"Type '{rpcServerType.FullName}' is not a valid RPC server type.");
        }

        var serviceType = rpcServerType.GetProperty(
                nameof(IRpcServerServiceType.ServiceType),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            ?.GetValue(null) as Type
            ?? throw new InvalidOperationException($"RPC server type '{rpcServerType.FullName}' does not expose static property '{nameof(IRpcServerServiceType.ServiceType)}'.");

        var handlerMappings = rpcServerType.GetProperty(
                nameof(IRpcServer.HandlerMappings),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            ?.GetValue(null) as IReadOnlyDictionary<string, RpcHandlerMapping>;

        if (handlerMappings is null)
        {
            throw new InvalidOperationException($"RPC server type '{rpcServerType.FullName}' does not expose static property '{nameof(IRpcServer.HandlerMappings)}'.");
        }

        return MapHttpEndpointCore(
            app,
            CreateHttpEndpointMappingKey(rpcServerType, prefix),
            serviceType,
            handlerMappings,
            prefix);
    }

    private static IEndpointRouteBuilder MapHttpEndpointCore(
        IEndpointRouteBuilder app,
        string mappingKey,
        Type serviceType,
        IReadOnlyDictionary<string, RpcHandlerMapping> handlerMappings,
        string? prefix)
    {
        var mappingState = app.ServiceProvider.GetService<HttpEndpointMappingState>();
        if (mappingState is not null && !mappingState.TryMarkMapped(mappingKey))
        {
            return app;
        }

        foreach (var (operationName, handlerMapping) in handlerMappings)
        {
            var method = serviceType.GetMethod(operationName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new InvalidOperationException($"RPC contract method '{serviceType.FullName}.{operationName}' was not found.");
            var isStream = TryGetStreamItemType(handlerMapping.ReturnType, out var streamItemType);
            var responseType = isStream ? streamItemType : handlerMapping.ReturnType;
            var endpoints = GetHttpEndpoints(method, operationName);
            foreach (var (verb, route) in endpoints)
            {
                var fullRoute = BuildRoute(prefix, route);
                var handler = CreateEndpointHandler(serviceType, handlerMapping.RequestType, operationName, verb, handlerMapping.ReturnType);
                var builder = verb switch
                {
                    HttpVerb.Get => app.MapGet(fullRoute, handler),
                    HttpVerb.Post => app.MapPost(fullRoute, handler),
                    HttpVerb.Put => app.MapPut(fullRoute, handler),
                    HttpVerb.Delete => app.MapDelete(fullRoute, handler),
                    HttpVerb.Patch => app.MapPatch(fullRoute, handler),
                    _ => throw new InvalidOperationException($"Unsupported HTTP verb '{verb}'.")
                };

                builder.WithMetadata(NofRpcHttpEndpointMetadata.Instance);
                ApplyDocumentation(builder, method);
                if (isStream)
                {
                    builder.Produces(statusCode: 200, contentType: "text/event-stream");
                }
                else
                {
                    builder.Produces(statusCode: 200, responseType: responseType);
                }
            }
        }

        return app;
    }

    private static string CreateHttpEndpointMappingKey(Type rpcServerType, string? prefix)
    {
        ArgumentNullException.ThrowIfNull(rpcServerType);

        return $"{rpcServerType.AssemblyQualifiedName}|{prefix ?? string.Empty}";
    }

    [RequiresUnreferencedCode("Endpoint handler creation uses runtime generic method binding.")]
    [RequiresDynamicCode("Endpoint handler creation uses runtime generic method instantiation.")]
    private static Delegate CreateEndpointHandler(Type serviceType, Type requestType, string operationName, HttpVerb verb, Type returnType)
    {
        MethodInfo templateMethod;
        Type[] genericArguments;
        if (TryGetStreamItemType(returnType, out var streamItemType))
        {
            templateMethod = verb is HttpVerb.Get or HttpVerb.Delete
                ? RequestHasHeaderBindings(requestType) ? _createHeaderAwareStreamQueryHandlerMethod : _createStreamQueryHandlerMethod
                : _createStreamBodyHandlerMethod;
            genericArguments = [serviceType, requestType, streamItemType];
        }
        else
        {
            templateMethod = verb is HttpVerb.Get or HttpVerb.Delete
                ? RequestHasHeaderBindings(requestType) ? _createHeaderAwareQueryHandlerMethod : _createQueryHandlerMethod
                : _createBodyHandlerMethod;
            genericArguments = [serviceType, requestType, returnType];
        }

        var genericMethod = templateMethod.MakeGenericMethod(genericArguments);
        return (Delegate)genericMethod.Invoke(null, [operationName])!;
    }

    [RequiresUnreferencedCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    private static Delegate CreateQueryHandlerCore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRequest,
        TResponse>(string operationName)
        where TService : class, IRpcService
    {
        async Task<Http.IResult> Handler(
            [AsParameters] TRequest request,
            HttpContext httpContext,
            [FromServices] HttpRequestInboundAdapter adapter,
            CancellationToken cancellationToken)
        {
            var execution = await adapter.InvokeAsync<TService, TRequest>(httpContext, operationName, request!, cancellationToken).ConfigureAwait(false);
            return CreateHttpResponse<TResponse>(execution.Response, httpContext);
        }

        return Handler;
    }

    [RequiresUnreferencedCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    private static Delegate CreateBodyHandlerCore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRequest,
        TResponse>(string operationName)
        where TService : class, IRpcService
    {
        async Task<Http.IResult> Handler(
            [FromBody] TRequest request,
            HttpContext httpContext,
            [FromServices] HttpRequestInboundAdapter adapter,
            CancellationToken cancellationToken)
        {
            var execution = await adapter.InvokeAsync<TService, TRequest>(httpContext, operationName, request!, cancellationToken).ConfigureAwait(false);
            return CreateHttpResponse<TResponse>(execution.Response, httpContext);
        }

        return Handler;
    }

    [RequiresUnreferencedCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    private static Delegate CreateHeaderAwareQueryHandlerCore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TRequest,
        TResponse>(string operationName)
        where TService : class, IRpcService
    {
        async Task<Http.IResult> Handler(
            HttpContext httpContext,
            [FromServices] HttpRequestInboundAdapter adapter,
            CancellationToken cancellationToken)
        {
            var request = CreateRequestFromQuery<TRequest>(httpContext);
            var execution = await adapter.InvokeAsync<TService, TRequest>(httpContext, operationName, request, cancellationToken).ConfigureAwait(false);
            return CreateHttpResponse<TResponse>(execution.Response, httpContext);
        }

        return Handler;
    }

    [RequiresUnreferencedCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    private static Delegate CreateStreamQueryHandlerCore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRequest,
        TItem>(string operationName)
        where TService : class, IRpcService
    {
        async Task<Http.IResult> Handler(
            [AsParameters] TRequest request,
            HttpContext httpContext,
            [FromServices] HttpRequestInboundAdapter adapter,
            CancellationToken cancellationToken)
        {
            var execution = await adapter.InvokeAsync<TService, TRequest>(httpContext, operationName, request!, cancellationToken).ConfigureAwait(false);
            return CreateStreamingResult<TItem>(execution.Response, httpContext);
        }

        return Handler;
    }

    [RequiresUnreferencedCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    private static Delegate CreateStreamBodyHandlerCore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRequest,
        TItem>(string operationName)
        where TService : class, IRpcService
    {
        async Task<Http.IResult> Handler(
            [FromBody] TRequest request,
            HttpContext httpContext,
            [FromServices] HttpRequestInboundAdapter adapter,
            CancellationToken cancellationToken)
        {
            var execution = await adapter.InvokeAsync<TService, TRequest>(httpContext, operationName, request!, cancellationToken).ConfigureAwait(false);
            return CreateStreamingResult<TItem>(execution.Response, httpContext);
        }

        return Handler;
    }

    [RequiresUnreferencedCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("Endpoint response writing may require runtime JSON serialization for transport bodies.")]
    private static Delegate CreateHeaderAwareStreamQueryHandlerCore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TRequest,
        TItem>(string operationName)
        where TService : class, IRpcService
    {
        async Task<Http.IResult> Handler(
            HttpContext httpContext,
            [FromServices] HttpRequestInboundAdapter adapter,
            CancellationToken cancellationToken)
        {
            var request = CreateRequestFromQuery<TRequest>(httpContext);
            var execution = await adapter.InvokeAsync<TService, TRequest>(httpContext, operationName, request, cancellationToken).ConfigureAwait(false);
            return CreateStreamingResult<TItem>(execution.Response, httpContext);
        }

        return Handler;
    }

    [RequiresUnreferencedCode("HTTP transport response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("HTTP transport response writing may require runtime JSON serialization for transport bodies.")]
    private static Http.IResult CreateStreamingResult<TItem>(
        NOF.Contract.IResult? response,
        HttpContext httpContext)
    {
        if (response is StreamingResult<TItem> streamingResult)
        {
            return new RpcStreamingHttpResult<TItem>(streamingResult);
        }

        if (response is not null)
        {
            return new RpcHttpResult(response, GetHttpStatusCode(httpContext));
        }

        throw new InvalidOperationException($"Streaming RPC endpoints must return '{typeof(StreamingResult<TItem>).FullName}'.");
    }

    [RequiresUnreferencedCode("HTTP transport response writing may require runtime JSON serialization for transport bodies.")]
    [RequiresDynamicCode("HTTP transport response writing may require runtime JSON serialization for transport bodies.")]
    private static Http.IResult CreateHttpResponse<TResponse>(
        NOF.Contract.IResult? transportResult,
        HttpContext httpContext)
    {
        if (transportResult is TResponse result)
        {
            return new RpcHttpResult((NOF.Contract.IResult)(object)result!, GetHttpStatusCode(httpContext));
        }

        if (transportResult is not null)
        {
            return new RpcHttpResult(transportResult, GetHttpStatusCode(httpContext));
        }

        throw new InvalidOperationException($"HTTP RPC endpoint returned '{transportResult?.GetType().FullName ?? "null"}' instead of '{typeof(TResponse).FullName}'.");
    }

    private static int GetHttpStatusCode(HttpContext httpContext)
        => httpContext.Items.TryGetValue(NOFInfrastructureConstants.Transport.Headers.HttpStatusCode, out var statusCode)
            && statusCode is int value
            ? value
            : StatusCodes.Status200OK;

    private static bool TryGetStreamItemType(Type returnType, [NotNullWhen(true)] out Type? streamItemType)
    {
        if (returnType.IsGenericType
            && returnType.GetGenericTypeDefinition() == typeof(StreamingResult<>))
        {
            streamItemType = returnType.GetGenericArguments()[0];
            return true;
        }

        streamItemType = null;
        return false;
    }

    private static bool RequestHasHeaderBindings([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type requestType)
        => requestType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Any(static property => property.GetCustomAttribute<NOF.Contract.FromHeaderAttribute>(inherit: true) is not null);

    private static TRequest CreateRequestFromQuery<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TRequest>(
        HttpContext httpContext)
    {
        var request = Activator.CreateInstance<TRequest>();
        foreach (var property in typeof(TRequest).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite || property.GetCustomAttribute<NOF.Contract.FromHeaderAttribute>(inherit: true) is not null)
            {
                continue;
            }

            if (!httpContext.Request.Query.TryGetValue(property.Name, out var values))
            {
                continue;
            }

            property.SetValue(request, ConvertQueryValue(values.ToString(), property.PropertyType));
        }

        return request;
    }

    private static object? ConvertQueryValue(string value, Type propertyType)
    {
        return TransportStringValueConverter.Convert(value, propertyType);
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
