using System.Collections.Concurrent;
using System.Reflection;

namespace NOF;

public enum HandlerKind
{
    Command,
    Event,
    Notification,
    RequestWithoutResponse,
    RequestWithResponse
}

public record HandlerInfo(HandlerKind Kind, Type HandlerType, Type MessageType, Type? ResponseType);

internal static class HandlerScanner
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyList<HandlerInfo>> Cache = new();
    public static IReadOnlyList<HandlerInfo> ScanHandlers(HashSet<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var allHandlers = assemblies.SelectMany(assembly => Cache.GetOrAdd(assembly, GetHandlerInfosInAssembly)).ToList();
        return allHandlers.AsReadOnly();

        static IReadOnlyList<HandlerInfo> GetHandlerInfosInAssembly(Assembly assembly)
        {
            var handlers = new List<HandlerInfo>();
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    var def = iface.GetGenericTypeDefinition();

                    if (def == typeof(ICommandHandler<>))
                    {
                        var messageType = iface.GenericTypeArguments[0];
                        handlers.Add(new HandlerInfo
                        (
                            Kind: HandlerKind.Command,
                            HandlerType: type,
                            MessageType: messageType,
                            ResponseType: null
                        ));
                    }
                    else if (def == typeof(IEventHandler<>))
                    {
                        var messageType = iface.GenericTypeArguments[0];
                        handlers.Add(new HandlerInfo
                        (
                            Kind: HandlerKind.Event,
                            HandlerType: type,
                            MessageType: messageType,
                            ResponseType: null
                        ));
                    }
                    else if (def == typeof(INotificationHandler<>))
                    {
                        var messageType = iface.GenericTypeArguments[0];
                        handlers.Add(new HandlerInfo
                        (
                            Kind: HandlerKind.Notification,
                            HandlerType: type,
                            MessageType: messageType,
                            ResponseType: null
                        ));
                    }
                    else if (def == typeof(IRequestHandler<,>))
                    {
                        var args = iface.GenericTypeArguments;
                        handlers.Add(new HandlerInfo
                        (
                            Kind: HandlerKind.RequestWithResponse,
                            HandlerType: type,
                            MessageType: args[0],
                            ResponseType: args[1]
                        ));
                    }
                    else if (def == typeof(IRequestHandler<>))
                    {
                        var messageType = iface.GenericTypeArguments[0];
                        handlers.Add(new HandlerInfo
                        (
                            Kind: HandlerKind.RequestWithoutResponse,
                            HandlerType: type,
                            MessageType: messageType,
                            ResponseType: null
                        ));
                    }
                }
            }
            return handlers.AsReadOnly();
        }
    }
}