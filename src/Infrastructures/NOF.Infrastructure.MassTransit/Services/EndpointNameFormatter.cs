using MassTransit;
using NOF.Infrastructure.Abstraction;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.MassTransit;

public class EndpointNameFormatter : DefaultEndpointNameFormatter
{
    private static readonly HashSet<Type> SupportedConsumerGenericTypes =
    [
        typeof(MassTransitRequestHandlerAdapter<,>),
        typeof(MassTransitRequestHandlerAdapter<,,>),
        typeof(MassTransitCommandHandlerAdapter<,>),
        typeof(MassTransitNotificationHandlerAdapter<,>)
    ];

    private readonly ConcurrentDictionary<Type, string> _nameCache = new();
    private readonly CommandHandlerInfos _commandInfos;
    private readonly RequestWithoutResponseHandlerInfos _requestInfos;
    private readonly RequestWithResponseHandlerInfos _requestWithResponseInfos;

    public EndpointNameFormatter(
        CommandHandlerInfos commandInfos,
        RequestWithoutResponseHandlerInfos requestInfos,
        RequestWithResponseHandlerInfos requestWithResponseInfos)
    {
        _commandInfos = commandInfos;
        _requestInfos = requestInfos;
        _requestWithResponseInfos = requestWithResponseInfos;
    }

    protected override string GetConsumerName(Type consumerType)
    {
        if (_nameCache.TryGetValue(consumerType, out var cached))
        {
            return cached;
        }

        if (!consumerType.IsGenericType || !SupportedConsumerGenericTypes.Contains(consumerType.GetGenericTypeDefinition()))
        {
            return _nameCache.GetOrAdd(consumerType, base.GetConsumerName(consumerType));
        }

        var handlerType = consumerType.GenericTypeArguments[0];
        var endpointName = _commandInfos.GetEndpointName(handlerType);

        return _nameCache.GetOrAdd(consumerType, endpointName);
    }
}
