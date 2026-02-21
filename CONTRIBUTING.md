# Contributing to NOF

Thank you for your interest in contributing to the **NOF (Neat Opinionated Framework)**! This guide covers everything you need to get started.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Style Guide](#coding-style-guide)
- [Commit Conventions](#commit-conventions)
- [Pull Request Process](#pull-request-process)
- [Issue Guidelines](#issue-guidelines)
- [Architecture Overview](#architecture-overview)

## Code of Conduct

Be respectful, constructive, and inclusive. We are committed to providing a welcoming experience for everyone.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- A C# IDE (Visual Studio 2026+, Rider, or VS Code with C# Dev Kit)
- Git

### Clone & Build

```bash
git clone https://github.com/XmmShp/NOF.git
cd NOF
dotnet restore
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Verify Formatting

```bash
dotnet format --verify-no-changes
```

The CI pipeline enforces formatting via `dotnet format`. Fix any violations before submitting a PR.

## Development Workflow

1. **Fork** the repository and create a branch from `develop`.
2. **Name your branch** using the convention: `feature/<short-description>`, `fix/<short-description>`, or `docs/<short-description>`.
3. Make your changes, ensuring all tests pass and formatting is clean.
4. Push to your fork and open a **Pull Request** against `develop`.

### Branch Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Stable releases; triggers CD (nightly NuGet + docs deployment) |
| `develop` | Integration branch; CI runs on push and PR |
| `feature/*` | New features |
| `fix/*` | Bug fixes |
| `docs/*` | Documentation-only changes |

## Coding Style Guide

NOF enforces a consistent coding style through `.editorconfig` and `dotnet format`. The key rules are summarized below.

### General

- **Indentation**: 4 spaces for C#; 2 spaces for XML/JSON/YAML.
- **Line endings**: LF (`\n`).
- **Charset**: UTF-8.
- **Trailing whitespace**: trimmed (except Markdown).
- **Final newline**: always insert.

### C# Conventions

#### Naming

| Symbol | Convention | Example |
|--------|-----------|---------|
| Types (class, struct, interface, enum) | PascalCase | `OrderService` |
| Interfaces | `I` + PascalCase | `IRequestHandler` |
| Public members (property, method, event) | PascalCase | `HandleAsync` |
| Private instance fields | `_camelCase` | `_repository` |
| Private static / readonly fields | PascalCase | `DefaultTimeout` |
| Constants | PascalCase | `MaxRetryCount` |
| Local variables & parameters | camelCase | `cancellationToken` |

#### Language Preferences

- Use `var` when the type is apparent.
- Prefer file-scoped namespaces (`namespace X;`) — enforced as **warning**.
- Prefer braces for all control-flow blocks — enforced as **warning**.
- Prefer pattern matching over `is`/`as` with null checks.
- Prefer expression-bodied members for single-line implementations.
- Prefer `using` declarations over `using` blocks.
- Place `using` directives outside the namespace.

#### Formatting

- **Allman-style braces**: opening brace on a new line for all constructs.
- One statement per line (no single-line compound statements).
- Space after keywords in control-flow (`if (`, `for (`), no space after casts.
- No `this.` qualification unless necessary.

### XML Documentation

- All public APIs in `src/` projects **must** have XML doc comments (`<summary>`, `<param>`, `<returns>`).
- `GenerateDocumentationFile` is enabled globally; missing docs will produce warnings.

### Project Conventions

- **Central Package Management**: all NuGet versions are declared in the root `Directory.Packages.props`. Never specify `Version` in individual `.csproj` files.
- **TreatWarningsAsErrors**: enabled for all `src/` projects.
- **EF Core Migrations**: files under `**/Migrations/*.cs` are marked as generated code and excluded from formatting.

## Commit Conventions

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short summary>
```

### Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting, no logic change |
| `refactor` | Code restructuring, no behavior change |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `chore` | Build, CI, tooling changes |

### Scopes

Use the package short name as scope: `domain`, `contract`, `application`, `hosting`, `core`, `efcore`, `masstransit`, `redis`, `jwt`, `generator`.

### Examples

```
feat(contract): add PatchRequest<T> abstraction
fix(efcore): resolve multi-tenant migration isolation
docs(hosting): update Quick Start example
test(generator): add SnowflakeId source generator tests
chore(ci): upgrade to .NET 10 SDK
```

## Pull Request Process

1. Ensure CI passes (build + format + tests).
2. Provide a clear PR description explaining **what** and **why**.
3. Reference related issues using `Closes #123` or `Fixes #123`.
4. Keep PRs focused — one logical change per PR.
5. Add or update tests for any behavioral changes.
6. Update documentation if the public API surface changes.
7. A maintainer will review and may request changes before merging.

### PR Checklist

- [ ] Branch is up-to-date with `develop`
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet build --configuration Release` succeeds with no warnings
- [ ] `dotnet test` passes
- [ ] New public APIs have XML documentation
- [ ] Commit messages follow conventional commits

## Issue Guidelines

- Use the provided [issue templates](https://github.com/XmmShp/NOF/issues/new/choose).
- Search existing issues before creating a new one.
- For bug reports: include package version, .NET version, and a minimal reproduction.
- For feature requests: describe the problem before proposing a solution.

## Architecture Overview

```
NOF.Domain              ← Domain entities, aggregate roots, events, [AutoInject]
NOF.Contract            ← IRequest, ICommand, INotification, Result<T>, [ExposeToHttpEndpoint]
NOF.Application         ← Handlers, state machines, caching, unit of work
NOF.Infrastructure.Core ← INOFAppBuilder, IStep pipeline, OpenTelemetry
NOF.Hosting.AspNetCore  ← ASP.NET Core host, endpoint mapping, middleware
NOF.Infrastructure.*    ← EF Core, MassTransit, Redis providers
NOF.Extensions.*        ← JWT authorization, optional features
```

### Key Patterns

- **Step Pipeline**: `IServiceRegistrationStep` and `IApplicationInitializationStep` with dependency ordering via `IAfter<T>` / `IBefore<T>`.
- **CQRS Messaging**: `IRequest` / `ICommand` / `INotification` with corresponding handlers.
- **Source Generators**: compile-time code generation for endpoint mapping, DI registration, failure definitions.
- **Transactional Outbox**: reliable messaging via EF Core inbox/outbox.

### Adding a New Infrastructure Package

1. Create the project under `src/Infrastructures/`.
2. Reference `NOF.Infrastructure.Abstraction` or `NOF.Infrastructure.Core`.
3. Implement registration via `IServiceRegistrationStep`.
4. Add the project to `NOF.slnx`.
5. Add NuGet versions to the root `Directory.Packages.props`.
6. Add pack command to `cd.yml` and `release.yml`.

## License

By contributing, you agree that your contributions will be licensed under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0).
