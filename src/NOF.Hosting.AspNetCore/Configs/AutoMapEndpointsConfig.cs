using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace NOF;

public class AutoMapEndpointsConfig<THostApplicationBuilder> : IEndpointConfig<THostApplicationBuilder>
    where THostApplicationBuilder : class, IHost, IEndpointRouteBuilder
{
    public Task ExecuteAsync(INOFAppBuilder<THostApplicationBuilder> builder, THostApplicationBuilder app)
    {
        foreach (var type in builder.Assemblies.SelectMany(a => a.GetTypes()))
        {
            if (!type.IsPublic || type.IsAbstract)
            {
                continue;
            }

            var exposes = type.GetCustomAttributes<ExposeToHttpEndpointAttribute>().ToArray();
            if (exposes.Length == 0)
            {
                continue;
            }

            (bool IsNonGenericRequest, Type? ResponseType) requestInfo = new();
            var ifaces = type.GetInterfaces();
            foreach (var iface in ifaces)
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IRequest<>))
                {
                    requestInfo.ResponseType = iface.GenericTypeArguments[0];
                }
                else if (iface == typeof(IRequest))
                {
                    requestInfo.IsNonGenericRequest = true;
                }
            }

            switch (requestInfo)
            {
                case { IsNonGenericRequest: false, ResponseType: null }:
                    throw new InvalidOperationException(
                        $"The type '{type.FullName}' does not implement '{nameof(IRequest)}'. " +
                        "Only types implementing IRequest can be used as request types.");
                case { IsNonGenericRequest: true, ResponseType: not null }:
                    throw new InvalidOperationException(
                        $"The type '{type.FullName}' is not allowed to implement both non-generic '{nameof(IRequest)}' " +
                        $"and generic '{typeof(IRequest<>).Name}' interfaces simultaneously. ");
            }

            var map = new Dictionary<FromType, Delegate>();
            IRequestDelegateFactory factory;
            if (requestInfo.IsNonGenericRequest)
            {
                var factoryType = typeof(RequestDelegateFactory<>).MakeGenericType(type);
                factory = (IRequestDelegateFactory)ActivatorUtilities.CreateInstance(app.Services, factoryType);
            }
            else
            {
                var factoryType = typeof(RequestDelegateFactory<,>).MakeGenericType(type, requestInfo.ResponseType!);
                factory = (IRequestDelegateFactory)ActivatorUtilities.CreateInstance(app.Services, factoryType);
            }

            foreach (var attr in exposes)
            {
                const string request = "Request";
                var operationName = attr.OperationName ??
                                    (type.Name.EndsWith(request, StringComparison.InvariantCultureIgnoreCase)
                                        ? type.Name[..^request.Length]
                                        : type.Name);

                var route = (attr.Route?.TrimEnd('/') ?? operationName).TrimStart('/');
                var fromType = attr.Method.IsUseBody() ? FromType.FromBody : FromType.FromQuery;
                if (!map.TryGetValue(fromType, out var del))
                {
                    del = factory.Create(fromType);
                    map[fromType] = del;
                }

                EndpointMapper.Map(app, attr.Method, route, del);
            }
        }

        return Task.CompletedTask;
    }
}

internal static class EndpointMapper
{
    private static readonly Dictionary<HttpVerb, Func<IEndpointRouteBuilder, string, Delegate, IEndpointConventionBuilder>> MapStrategies = new()
    {
        [HttpVerb.Get] = (app, route, h) => app.MapGet(route, h),
        [HttpVerb.Post] = (app, route, h) => app.MapPost(route, h),
        [HttpVerb.Put] = (app, route, h) => app.MapPut(route, h),
        [HttpVerb.Delete] = (app, route, h) => app.MapDelete(route, h),
        [HttpVerb.Patch] = (app, route, h) => app.MapPatch(route, h),
    };

    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder app, HttpVerb verb, string route, Delegate handler)
    {
        return MapStrategies.TryGetValue(verb, out var strategy)
            ? strategy(app, route, handler)
            : throw new InvalidOperationException($"Unsupported HTTP verb: {verb}");
    }
}