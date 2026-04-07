# Handler Registration

## Current Model (Sample-Aligned)

NOF no longer depends on calling `AddAllHandlers()` or `Add*AutoInjectServices()` from user code.

The runtime model is:

1. Source generators emit assembly-level initializers (`[assembly: AssemblyInitializeAttribute<...>]`).
2. Initializers write metadata into static registries:
- `AutoInjectRegistry`
- `RequestHandlerRegistry`
- `HandlerRegistry`
3. At startup, `builder.AddApplicationPart(assembly)` executes those initializers.
4. Infrastructure registration steps read registries and add services:
- `AutoInjectServiceRegistrationStep`
- `RequestHandlerServiceRegistrationStep`
- `HandlerServiceRegistrationStep`

This is the same pattern used by the sample app:

```csharp
builder.AddApplicationPart(typeof(NOFSampleService).Assembly)
    .AddApplicationPart(typeof(JwtAuthorityService).Assembly);
```

## Practical Guidance

- If handlers/services are not discovered, first check that the assembly containing them is added via `AddApplicationPart(...)`.
- Keep RPC contracts (`IRpcService`) and their implementations in assemblies that are loaded as application parts.
- For transport integrations (for example RabbitMQ), registrations come from `HandlerInfos`, which is populated from `HandlerRegistry` during startup steps.
