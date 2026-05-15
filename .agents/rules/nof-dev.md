---
trigger: always_on
---

# NOF Framework Development Rules

Use this file when contributing to the NOF framework itself.

## Repository Layout

- `src/` framework source packages.
- `src/Hostings/` host-specific packages.
- `src/Infrastructures/` infrastructure providers.
- `src/Extensions/` optional extension packages.
- `sample/` runnable sample app.
- `tests/` test projects. Most package and host tests live in `tests/NOF.*.Tests`, with extension tests in `tests/Extensions/`, infrastructure provider tests in `tests/Infrastructures/`, and shared helpers in `tests/Common/`.
- `docs/` DocFX documentation.

## Tech Stack

- .NET 10 / C# 14 features (`extension` blocks are used in this repo).
- Central Package Management via root `Directory.Packages.props`.
- Roslyn incremental generators.
- xUnit + Moq.

## Key Patterns

- CQRS: `IRpcService`, `RpcServer<TService>`, `CommandHandler<T>`, `NotificationHandler<T>`, `InMemoryEventHandler<T>`, `ICommandSender`, `INotificationPublisher`, `IEventPublisher`.
- Commands and notifications are plain payload types; discovery comes from handler base types, not marker interfaces.
- Step pipeline: registration and initialization steps with `IAfter<T>` / `IBefore<T>`.
- Source-gen attributes: `[AutoInject]`, `[HttpEndpoint]`, `[Failure]`, `[Mappable]`, `[NewableValueObject]`.
- Transactional outbox: `ICommandSender.DeferSend(...)` / `INotificationPublisher.DeferPublish(...)`.
- `Registry` is builder-owned and first-class; do not document or implement it as hidden builder property-bag state.
- `AutoInjectRegistry` stores `ServiceDescriptor` entries directly.
- `TypeResolver` is DI-managed; do not reintroduce `TypeRegistry` or other mutable process-wide lookup singletons.
- Ambient convenience APIs such as `Mapper`, `IdGenerator`, and `EventPublisher` are async-flow scoped and must keep explicit overloads available.
- Prefer DI singletons over mutable `static` state when data must be shared per host or per builder.

## Coding Rules

- File-scoped namespaces.
- Braces on all control-flow.
- Allman style.
- Public APIs in `src/` require XML comments.
- Do not place NuGet versions in individual `.csproj` files.

## Build Commands

```bash
dotnet restore
dotnet build --configuration Release
dotnet test
dotnet format --verify-no-changes
```

## Change Checklist

Before considering work done, verify all applicable items:

1. Tests updated:
- Parent package tests and source generator tests are kept together (for example `NOF.Domain` + `NOF.Domain.SourceGenerator` in `tests/NOF.Domain.Tests`).
- Extension package tests are added under `tests/Extensions/*`.

2. Sample app still compiles and demonstrates changed behavior.

3. Docs updated:
- XML docs on public APIs.
- `README.md`, `docs/`, and `.agents/*` where relevant.

4. CI/CD updated when adding package/test projects.

5. Central package management respected (versions only in root `Directory.Packages.props`).
