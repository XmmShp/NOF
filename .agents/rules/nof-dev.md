---
trigger: always_on
---

# NOF Framework Development Rules

> **Audience**: Human developers AND AI coding assistants contributing to the NOF framework itself.
> If you are building an application that USES NOF, see `rules/app-dev.md` instead.

You are working in the **NOF (Neat Opinionated Framework)** repository — a modular, convention-driven .NET application framework.

## Repository Layout

- `src/` — Framework source packages (Domain, Contract, Application, Infrastructure, Hosting, Extensions)
- `src/*SourceGenerator/` — Roslyn source generators (compile-time code generation)
- `sample/` — Sample application demonstrating NOF usage
- `tests/` — xUnit tests (Contract, Integration, SourceGenerator)
- `docs/` — DocFX API documentation source
- `.github/workflows/` — CI (develop), CD (main → nightly NuGet + docs), Release (tags)

## Tech Stack

- .NET 10, C# 14 (preview features, `extension` syntax)
- Central Package Management (`Directory.Packages.props` at root)
- Source Generators (Microsoft.CodeAnalysis / Roslyn)
- MassTransit + RabbitMQ, EF Core 10 + PostgreSQL, StackExchange.Redis
- OpenTelemetry, JWT/OIDC authorization
- xUnit + FluentAssertions + Moq for testing
- DocFX for API documentation
- GitHub Actions for CI/CD

## Key Patterns

- **CQRS**: `IRequest`/`ICommand`/`INotification` with typed handlers
- **Step Pipeline**: `IServiceRegistrationStep` / `IApplicationInitializationStep` with `IAfter<T>`/`IBefore<T>` ordering
- **Source Gen Attributes**: `[AutoInject]`, `[ExposeToHttpEndpoint]`, `[Failure]`, `[ValueObject]`
- **Transactional Outbox**: `IDeferredNotificationPublisher` via EF Core

## Coding Rules

- File-scoped namespaces (enforced as warning)
- Braces required on all control-flow blocks (enforced as warning)
- Allman-style braces
- Private instance fields: `_camelCase`; static/readonly/const: `PascalCase`
- All public APIs in `src/` must have XML doc comments
- `TreatWarningsAsErrors` enabled for `src/` projects
- NuGet versions only in root `Directory.Packages.props`
- Conventional Commits: `<type>(<scope>): <summary>`

## Build Commands

```bash
dotnet restore
dotnet build --configuration Release
dotnet test
dotnet format --verify-no-changes
```

---

## ⚠️ CRITICAL: Complete Change Checklist

> **For both human developers and AI agents**: Every change to the NOF framework MUST consider ALL of the following areas. Do NOT only update the source code — incomplete changes cause CI failures, documentation drift, and broken samples.

### Before You Consider a Change "Done"

Ask yourself (or your AI agent) these questions:

1. **Tests** — Did you add or update tests?
   - Unit tests in `tests/NOF.Contract.Tests/` or `tests/NOF.SourceGenerator.Tests/`
   - Integration tests in `tests/NOF.Integration.Tests/`
   - Source generator tests if you changed any generator
   - Run `dotnet test` to verify

2. **Sample Application** — Does `sample/` still compile and demonstrate the feature correctly?
   - If you added a new API or changed behavior, update the sample to showcase it
   - If you renamed or removed an API, update the sample to avoid compile errors
   - Run `dotnet build sample/NOF.Sample.AppHost/NOF.Sample.AppHost.csproj` to verify

3. **Documentation** — Did you update the docs?
   - XML doc comments on all new/changed public APIs (enforced by `GenerateDocumentationFile`)
   - `docs/` — DocFX API documentation if the public surface changed
   - `README.md` — Package table, Quick Start, Architecture diagram if affected
   - `CONTRIBUTING.md` — If conventions or processes changed
   - `.github/copilot-instructions.md` — If abstractions, patterns, or usage changed

4. **CI/CD Pipelines** — Do the GitHub Actions workflows need updating?
   - `.github/workflows/ci.yml` — If you added a new test project or changed build steps
   - `.github/workflows/cd.yml` — If you added a new NuGet package (add `dotnet pack` command)
   - `.github/workflows/release.yml` — Same as cd.yml for release builds

5. **Agent Instructions** — Did you update the AI agent files?
   - `.agents/rules/nof-dev.md` (this file) — If framework conventions changed
   - `.agents/rules/app-dev.md` — If usage patterns for app developers changed
   - `.agents/workflows/nof-dev/*` — If framework development workflows changed
   - `.agents/workflows/app-dev/*` — If app development workflows changed
   - `.agents/skills/nof-app-development.md` — If the app development skill needs updating
   - `.github/copilot-instructions.md` — Keep in sync with agent rules

6. **Central Package Management** — If you added new NuGet dependencies:
   - Add version to root `Directory.Packages.props`
   - Never specify `Version` in individual `.csproj` files

### Quick Self-Check (Copy-Paste for PR Description)

```markdown
- [ ] Source code updated
- [ ] Tests added/updated (`dotnet test` passes)
- [ ] Sample application updated (compiles and demonstrates the change)
- [ ] XML doc comments on all new public APIs
- [ ] Documentation updated (README / docs/ / CONTRIBUTING.md as needed)
- [ ] CI/CD workflows updated (if new package or test project)
- [ ] Agent instructions updated (.agents/ and .github/copilot-instructions.md)
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet build --configuration Release` succeeds with no warnings
- [ ] Commit messages follow conventional commits
```
