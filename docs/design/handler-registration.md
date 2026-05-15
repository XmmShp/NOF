# Handler Registration

## Current Model (Sample-Aligned)

NOF no longer depends on calling `AddAllHandlers()` or `Add*AutoInjectServices()` from user code.

The runtime model is:

1. Source generators emit assembly-level initializers (`[assembly: AssemblyInitializeAttribute<...>]`).
2. Initializers write metadata into the builder-owned `Registry`:
- `Registry.AutoInjectRegistry`
- `Registry.RequestHandlerRegistry`
- `Registry.CommandHandlerRegistry`
- `Registry.NotificationHandlerRegistry`
- `Registry.EventHandlerRegistry`
- `Registry.MapperRegistry`
3. At startup, `builder.AddApplicationPart(assembly)` executes those initializers.
4. Registry collections freeze on first read; indexed registries build their indexes when frozen.
5. Registration steps wire the runtime:
- `AutoInjectServiceRegistrationStep`
- `RequestHandlerServiceRegistrationStep`
- `HandlerServiceRegistrationStep`

This is the same pattern used by the sample app:

```csharp
builder.AddApplicationPart(typeof(NOFSampleService).Assembly)
    .AddApplicationPart(typeof(JwtAuthorityService).Assembly);
```

## Practical Guidance

- If handlers or mappers are not discovered, first check that the assembly containing them is added via `AddApplicationPart(...)`.
- Keep RPC contracts (`IRpcService`) and their implementations in assemblies that are loaded as application parts.
- Transport integrations such as RabbitMQ consume the frozen handler registries, so stale or missing application parts will prevent consumer registration.
- `AutoInjectRegistry` stores native `ServiceDescriptor` instances; there is no separate `AutoInjectServiceRegistration` model anymore.
