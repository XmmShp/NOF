using NOF.Application;
using System.Collections.Concurrent;

namespace NOF.Abstraction;

public static class NOFApplicationRegistryExtensions
{
    private static readonly ConcurrentBag<CommandHandlerRegistration> _commandHandlerRegistrations = [];
    private static readonly ConcurrentBag<NotificationHandlerRegistration> _notificationHandlerRegistrations = [];
    private static readonly ConcurrentBag<RequestHandlerRegistration> _requestHandlerRegistrations = [];
    private static readonly ConcurrentBag<RpcServerRegistration> _rpcServerRegistrations = [];
    private static readonly ConcurrentBag<MapperRegistration> _mapperRegistrations = [];

    extension(Registry)
    {
        public static ConcurrentBag<CommandHandlerRegistration> CommandHandlerRegistrations => _commandHandlerRegistrations;

        public static ConcurrentBag<NotificationHandlerRegistration> NotificationHandlerRegistrations => _notificationHandlerRegistrations;

        public static ConcurrentBag<RequestHandlerRegistration> RequestHandlerRegistrations => _requestHandlerRegistrations;

        public static ConcurrentBag<RpcServerRegistration> RpcServerRegistrations => _rpcServerRegistrations;

        public static ConcurrentBag<MapperRegistration> MapperRegistrations => _mapperRegistrations;
    }
}
