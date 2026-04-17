using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NOF.Annotation;
using NOF.Application;
using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting.AspNetCore;

public static partial class NOFHostingAspNetCoreExtensions
{
    extension(IEndpointRouteBuilder app)
    {
        [RequiresUnreferencedCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
        [RequiresDynamicCode("Endpoint mapping uses reflection on delegate signatures and service contracts.")]
        public IEndpointRouteBuilder MapHttpEndpoint<TRpcServer>(string? prefix = null)
            where TRpcServer : RpcServer, IRpcServer
        {
            ArgumentNullException.ThrowIfNull(app);

            // Ensure registry entries generated for this assembly are applied before mapping.
            InitializeAssembly(typeof(TRpcServer).Assembly);

            var infos = app.ServiceProvider.GetRequiredService<RpcHttpEndpointHandlerInfos>();
            var serviceType = TRpcServer.ServiceType;
            foreach (var method in serviceType.GetMethods())
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                var operationName = method.Name;
                if (!infos.TryGet(serviceType, operationName, out var entry))
                {
                    throw new InvalidOperationException(
                        $"HTTP endpoint handler is not registered for '{serviceType.FullName}.{operationName}'. " +
                        $"Ensure MapHttpEndpoint<{typeof(TRpcServer).Name}> was included in a project with NOF.Hosting.AspNetCore.SourceGenerator enabled.");
                }

                var endpoints = GetHttpEndpoints(method, operationName);
                foreach (var (verb, route) in endpoints)
                {
                    var fullRoute = BuildRoute(prefix, route);

                    var builder = verb switch
                    {
                        HttpVerb.Get => app.MapGet(fullRoute, entry.Handler),
                        HttpVerb.Post => app.MapPost(fullRoute, entry.Handler),
                        HttpVerb.Put => app.MapPut(fullRoute, entry.Handler),
                        HttpVerb.Delete => app.MapDelete(fullRoute, entry.Handler),
                        HttpVerb.Patch => app.MapPatch(fullRoute, entry.Handler),
                        _ => throw new InvalidOperationException($"Unsupported HTTP verb '{verb}'.")
                    };

                    ApplyDocumentation(builder, method);

                    if (entry.ReturnType != typeof(void))
                    {
                        builder.Produces(statusCode: 200, responseType: entry.ReturnType);
                    }
                }
            }

            return app;
        }
    }

    private static void InitializeAssembly(Assembly assembly)
    {
        foreach (var attribute in assembly.GetCustomAttributes<AssemblyInitializeAttribute>())
        {
            attribute.InitializeMethod();
        }
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

            var route = string.IsNullOrWhiteSpace(attr.Value) ? defaultRoute : attr.Value!;
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
        route ??= string.Empty;

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
