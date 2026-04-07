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
- `tests/` test projects. Extension package tests live in `tests/Extensions/`.
- `docs/` DocFX documentation.

## Tech Stack

- .NET 10 / C# 14 preview features (`extension` blocks are used in this repo).
- Central Package Management via root `Directory.Packages.props`.
- Roslyn incremental generators.
- xUnit + FluentAssertions + Moq.

## Key Patterns

- CQRS: `IRpcService`, `ICommand`, `INotification`, `IEvent`.
- Step pipeline: registration and initialization steps with `IAfter<T>` / `IBefore<T>`.
- Source-gen attributes: `[AutoInject]`, `[PublicApi]`, `[HttpEndpoint]`, `[GenerateService]`, `[Failure]`, `[Mappable]`, `[NewableValueObject]`.
- Transactional outbox: `IDeferredNotificationPublisher` / `IDeferredCommandSender`.

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
