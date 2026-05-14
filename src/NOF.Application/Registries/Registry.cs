using NOF.Application;

namespace NOF.Abstraction;

public static class NOFApplicationRegistryExtensions
{
    private const string CommandHandlerRegistrationsKey = "NOF.Application.CommandHandlerRegistrations";
    private const string NotificationHandlerRegistrationsKey = "NOF.Application.NotificationHandlerRegistrations";
    private const string RequestHandlerRegistrationsKey = "NOF.Application.RequestHandlerRegistrations";
    private const string RpcServerRegistrationsKey = "NOF.Application.RpcServerRegistrations";
    private const string MapperRegistrationsKey = "NOF.Application.MapperRegistrations";

    extension(Registry registry)
    {
        public List<CommandHandlerRegistration> CommandHandlerRegistrations
            => registry.GetOrAdd(CommandHandlerRegistrationsKey, static () => new List<CommandHandlerRegistration>());

        public List<NotificationHandlerRegistration> NotificationHandlerRegistrations
            => registry.GetOrAdd(NotificationHandlerRegistrationsKey, static () => new List<NotificationHandlerRegistration>());

        public List<RequestHandlerRegistration> RequestHandlerRegistrations
            => registry.GetOrAdd(RequestHandlerRegistrationsKey, static () => new List<RequestHandlerRegistration>());

        public List<RpcServerRegistration> RpcServerRegistrations
            => registry.GetOrAdd(RpcServerRegistrationsKey, static () => new List<RpcServerRegistration>());

        public List<MapperRegistration> MapperRegistrations
            => registry.GetOrAdd(MapperRegistrationsKey, static () => new List<MapperRegistration>());
    }
}
