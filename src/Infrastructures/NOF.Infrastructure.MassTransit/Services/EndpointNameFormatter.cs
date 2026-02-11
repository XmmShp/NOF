using MassTransit;
using NOF.Infrastructure.Core;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.MassTransit;

public class EndpointNameFormatter : DefaultEndpointNameFormatter
{
    private static readonly HashSet<Type> SupportedConsumerGenericTypes =
    [
        typeof(MassTransitRequestHandlerAdapter<,>),
        typeof(MassTransitRequestHandlerAdapter<,,>),
        typeof(MassTransitEventHandlerAdapter<,>),
        typeof(MassTransitCommandHandlerAdapter<,>),
        typeof(MassTransitNotificationHandlerAdapter<,>)
    ];

    private readonly ConcurrentDictionary<Type, string> _nameCache = new();
    private readonly IEndpointNameProvider _nameProvider;

    public EndpointNameFormatter(IEndpointNameProvider nameProvider)
    {
        ArgumentNullException.ThrowIfNull(nameProvider);
        _nameProvider = nameProvider;
    }

    protected override string GetConsumerName(Type consumerType)
    {
        if (_nameCache.TryGetValue(consumerType, out var cached))
        {
            return cached;
        }

        if (!consumerType.IsGenericType || !SupportedConsumerGenericTypes.Contains(consumerType.GetGenericTypeDefinition()))
        {
            var fallback = _nameProvider.GetEndpointName(consumerType);
            return _nameCache.GetOrAdd(consumerType, fallback);
        }

        var handlerType = consumerType.GenericTypeArguments[0];
        var endpointName = _nameProvider.GetEndpointName(handlerType);

        return _nameCache.GetOrAdd(consumerType, endpointName);
    }

    protected override string GetMessageName(Type type)
    {
        return _nameProvider.GetEndpointName(type);
    }
}
