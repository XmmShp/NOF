# `.agents` - NOF Agent Guidance

> Audience: human developers and AI coding assistants.

This directory contains the repository-local guidance for contributing to NOF and for building applications on NOF.

## Source of Truth

When guidance disagrees with the repository, use this order:

1. public contracts and implementations under `src/`
2. executable usage under `sample/` and assertions under `tests/` / `sample-tests/`
3. `.agents/` guidance

Keep `.agents/` synchronized whenever a public API, generated shape, package boundary, runtime registration path, or repository layout changes.

Current runtime expectations that guidance must preserve:

- application parts execute source-generated assembly initializers against `IServiceCollection`
- handler, mapping, event, and RPC registries are DI singleton instances and freeze on first read
- RPC contracts declare exactly one transport and RPC servers are registered explicitly with `AddRpcServer<T>()`
- RPC, command, and notification boundaries pass `Context` explicitly
- application persistence uses `IDbContext` and `IRepository<T>`; EF Core and NHibernate are provider packages
- `Mapper`, `IdGenerator`, and `EventPublisher` are async-flow conveniences with explicit dependency paths available

## Directory Structure

```text
.agents/
  rules/
    nof-dev.md
    app-dev.md
  workflows/
    nof-dev/
      add-infrastructure-package.md
      add-source-generator.md
      add-step.md
      run-ci-locally.md
    app-dev/
      scaffold-nof-app.md
      add-domain-entity.md
      add-domain-event-handler.md
      add-efcore-database.md
      add-handler.md
      add-oidc-auth.md
      add-rabbitmq-messaging.md
      add-redis-caching.md
      add-request-handler.md
  skills/
    nof-app-development/
      SKILL.md
      references/
        infrastructure.md
        recipes.md
```

## Which File To Use

| Scenario | Read Rules | Use Workflows | Use Skill |
|---|---|---|---|
| Contributing to NOF (`src/`, `tests/`, CI/CD) | `rules/nof-dev.md` | `workflows/nof-dev/*` | N/A |
| Building an app with NOF | `rules/app-dev.md` | `workflows/app-dev/*` | `skills/nof-app-development/SKILL.md` |

## Test Layout

- Core, hosting, UI, and test-helper tests live under `tests/NOF.*.Tests`.
- Provider-specific tests live under `tests/Infrastructures/*`; currently RabbitMQ has a dedicated provider test project.
- Shared source-generator test helpers live under `tests/Common/SourceGenerator`.
- Source-generator tests are normally colocated with the parent runtime package test project.
- Sample integration tests live under `sample-tests/NOF.Sample.Tests`.
