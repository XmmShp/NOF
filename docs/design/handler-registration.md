# Handler Registration

## Current Model (Sample-Aligned)

NOF no longer depends on calling `AddAllHandlers()` or `Add*AutoInjectServices()` from user code.

The runtime model is:

1. Source generators emit assembly-level initializers (`[assembly: AssemblyInitializeAttribute<...>]`).
2. Initializers write metadata into static registries:
- `Registry.AutoInjectRegistrations`
- `Registry.RequestHandlerRegistrations`
- `Registry.CommandHandlerRegistrations`
- `Registry.NotificationHandlerRegistrations`
- `Registry.EventHandlerRegistrations`
- `Registry.MapperRegistrations`
3. At startup, `builder.AddApplicationPart(assembly)` executes those initializers.
4. Info singletons materialize and freeze registrations on first read:
- `AutoInjectInfos`
- `RequestHandlerInfos`
- `CommandHandlerInfos`
- `NotificationHandlerInfos`
- `EventHandlerInfos`
- `MapperInfos`
5. Registration and initialization steps wire the runtime:
- `AutoInjectServiceRegistrationStep`
- `RequestHandlerServiceRegistrationStep`
- `HandlerServiceRegistrationStep`
- `MapperInitializationStep`

This is the same pattern used by the sample app:

```csharp
builder.AddApplicationPart(typeof(NOFSampleService).Assembly)
    .AddApplicationPart(typeof(JwtAuthorityService).Assembly);
```

## Practical Guidance

- If handlers or mappers are not discovered, first check that the assembly containing them is added via `AddApplicationPart(...)`.
- Keep RPC contracts (`IRpcService`) and their implementations in assemblies that are loaded as application parts.
- Transport integrations such as RabbitMQ consume `CommandHandlerInfos` and `NotificationHandlerInfos`, so stale or missing application parts will prevent consumer registration.
