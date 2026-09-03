---
trigger: always_on
---

# NOF Framework Development Rules

Use this file when contributing to the NOF framework itself.

## Repository Layout

- `src/`: runtime packages, source generators, code fixes, hosting integrations, extensions, and providers.
- `src/Hostings/`: ASP.NET Core, Blazor WebAssembly, Console, and MAUI hosts.
- `src/Infrastructures/`: EF Core, NHibernate, RabbitMQ, and StackExchange.Redis providers.
- `src/Extensions/`: optional feature packages, currently the ASP.NET Core OIDC server.
- `sample/`: runnable multi-project sample and Aspire app host.
- `sample-tests/`: sample integration tests.
- `tests/NOF.*.Tests`: core, hosting, UI, and test-helper tests.
- `tests/Infrastructures/*`: provider-specific tests.
- `tests/Common/SourceGenerator`: shared generator test helpers.
- `docs/`: DocFX content; `website/`: homepage application.

## Tech Stack

- .NET 10 and current C# (`extension` blocks are used throughout the repository).
- Central Package Management through root `Directory.Packages.props`; `sample/Directory.Packages.props` imports it for sample-only versions.
- Roslyn incremental generators and analyzers targeting `netstandard2.0`.
- xUnit and Moq.

## Current Runtime Patterns

- RPC: `IRpcService`, one `[TransportOverHttp(...)]` or `[TransportOverMemory]` declaration, `RpcServer<TService>`, generated clients, and `AddRpcServer<TServer>()`.
- Messaging: plain payload types plus `CommandHandler<T>` / `NotificationHandler<T>`; all handler and dispatch APIs receive `Context`.
- In-memory events: `InMemoryEventHandler<T>`, `IEventPublisher`, and optional `PublishAsEvent()` ambient convenience.
- Transactional outbox: asynchronous `DeferSendAsync(...)` / `DeferPublishAsync(...)`, plus ordered variants, followed by `IDbContext.SaveChangesAsync()`.
- Persistence: application-facing `IDbContext`, `IDbContextFactory`, and `IRepository<T>`; EF Core and NHibernate adapters live in provider packages.
- Initialization: `IApplicationInitializationStep` instances are registered in `IServiceCollection` and ordered by `Compare(...)` returning `TopologyComparison`.
- Source generation: `[AutoInject]`, `[Failure]`, `[Mappable<TSource, TDestination>]`, `[NewableValueObject]`, `IValueObject<T>`, handler registration, and RPC client/server generation.
- Application parts: `AddApplicationPart(assembly)` runs generated `AssemblyInitializeAttribute` initializers against the current `IServiceCollection`.
- Registries: `EventHandlerRegistry`, `MappingRegistry`, `CommandHandlerRegistry`, `NotificationHandlerRegistry`, and `RpcServerRegistry` are singleton instances stored in DI and freeze on first read. Do not reintroduce a process-wide type registry.
- Ambient helpers: `Mapper`, `IdGenerator`, and `EventPublisher` are scoped through daemon-service resolution; explicit APIs remain the primary runtime contracts.

## Coding Rules

- Use file-scoped namespaces, Allman braces, and braces for every control-flow body.
- Add XML documentation for public APIs under `src/`.
- Do not put NuGet versions in individual `.csproj` files.
- Register services directly through `IServiceCollection`; there is no service-registration-step pipeline.
- Preserve Native AOT annotations and analyzer isolation: a generator must not depend on another generator's output being visible as input.

## Build Commands

```bash
dotnet restore NOF.slnx
dotnet format --verify-no-changes --verbosity diagnostic
dotnet build NOF.slnx --configuration Release --no-restore
dotnet test NOF.slnx --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage"
```

The GitHub matrix installs the MAUI workload on Windows before restore. See `workflows/nof-dev/run-ci-locally.md` for the exact sequence.

## Change Checklist

Before considering work done, verify all applicable items:

1. Put runtime and generator tests in the owning package test project; put provider tests under `tests/Infrastructures/` when a dedicated project is warranted.
2. Keep the sample and `sample-tests` compiling when public behavior changes.
3. Update public XML docs, `README.md`, `docs/`, and `.agents/` as applicable.
4. Add new projects to `NOF.slnx`; update CI/CD only when its discovery or build inputs require it.
5. Add dependency versions only to root `Directory.Packages.props`, or to `sample/Directory.Packages.props` for sample-only packages.
6. Run `dotnet format`, review any changes, then run the relevant builds and tests.
