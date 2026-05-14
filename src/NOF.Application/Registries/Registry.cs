using NOF.Application;

namespace NOF.Abstraction;

public static class NOFApplicationRegistryExtensions
{
    private const string CommandHandlerRegistryKey = "NOF.Application.CommandHandlerRegistry";
    private const string NotificationHandlerRegistryKey = "NOF.Application.NotificationHandlerRegistry";
    private const string RequestHandlerRegistryKey = "NOF.Application.RequestHandlerRegistry";
    private const string RpcServerRegistryKey = "NOF.Application.RpcServerRegistry";
    private const string MapperRegistryKey = "NOF.Application.MapperRegistry";

    extension(Registry registry)
    {
        public CommandHandlerRegistry CommandHandlerRegistry
            => registry.GetOrAdd(CommandHandlerRegistryKey, static () => new CommandHandlerRegistry());

        public NotificationHandlerRegistry NotificationHandlerRegistry
            => registry.GetOrAdd(NotificationHandlerRegistryKey, static () => new NotificationHandlerRegistry());

        public RequestHandlerRegistry RequestHandlerRegistry
            => registry.GetOrAdd(RequestHandlerRegistryKey, static () => new RequestHandlerRegistry());

        public RpcServerRegistry RpcServerRegistry
            => registry.GetOrAdd(RpcServerRegistryKey, static () => new RpcServerRegistry());

        public MapperRegistry MapperRegistry
            => registry.GetOrAdd(MapperRegistryKey, static () => new MapperRegistry());
    }
}
