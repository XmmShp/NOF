using MassTransit;
using System.Collections.Concurrent;

namespace NOF;

public class EndpointNameFormatter : DefaultEndpointNameFormatter
{
    private static readonly ConcurrentDictionary<Type, string> NameCache = new();
    private static readonly HashSet<Type> SupportedConsumerGenericTypes =
    [
        typeof(MassTransitRequestHandlerAdapter<,>),
        typeof(MassTransitRequestHandlerAdapter<,,>),
        typeof(MassTransitEventHandlerAdapter<,>),
        typeof(MassTransitCommandHandlerAdapter<,>),
        typeof(MassTransitNotificationHandlerAdapter<,>)
    ];

    public new static EndpointNameFormatter Instance { get; } = new();

    protected override string GetConsumerName(Type consumerType)
    {
        if (NameCache.TryGetValue(consumerType, out var cached))
        {
            return cached;
        }

        if (!consumerType.IsGenericType || !SupportedConsumerGenericTypes.Contains(consumerType.GetGenericTypeDefinition()))
        {
            var fallback = consumerType.GetEndpointName();
            return NameCache.GetOrAdd(consumerType, fallback);
        }

        var handlerType = consumerType.GenericTypeArguments[0];
        var endpointName = handlerType.GetEndpointName();

        return NameCache.GetOrAdd(consumerType, endpointName);
    }

    protected override string GetMessageName(Type type)
    {
        return type.GetEndpointName();
    }
}
