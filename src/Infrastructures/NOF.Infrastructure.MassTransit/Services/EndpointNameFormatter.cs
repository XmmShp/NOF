using MassTransit;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.MassTransit;

public class EndpointNameFormatter : DefaultEndpointNameFormatter
{
    private static readonly HashSet<Type> _supportedConsumerGenericTypes =
    [
        typeof(MassTransitRequestHandlerAdapter<,>),
        typeof(MassTransitRequestHandlerAdapter<,,>),
        typeof(MassTransitCommandHandlerAdapter<,>),
        typeof(MassTransitNotificationHandlerAdapter<,>)
    ];

    private readonly ConcurrentDictionary<Type, string> _nameCache = new();
    private readonly HandlerInfos _infos;

    public EndpointNameFormatter(HandlerInfos infos)
    {
        _infos = infos;
    }

    protected override string GetConsumerName(Type consumerType)
    {
        if (_nameCache.TryGetValue(consumerType, out var cached))
        {
            return cached;
        }

        if (!consumerType.IsGenericType || !_supportedConsumerGenericTypes.Contains(consumerType.GetGenericTypeDefinition()))
        {
            return _nameCache.GetOrAdd(consumerType, base.GetConsumerName(consumerType));
        }

        var handlerType = consumerType.GenericTypeArguments[0];
        var endpointName = _infos.GetEndpointName(handlerType);

        return _nameCache.GetOrAdd(consumerType, endpointName);
    }
}
