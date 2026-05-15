# .agents - AI Agent Instructions for NOF

> Audience: human developers and AI coding assistants.

This directory contains shared instructions for working with the NOF codebase.
When framework behavior changes, keep these files in sync with `src/`, `tests/`, and sample usage.

Current framework-wide architectural expectations that agent guidance must preserve:

- prefer builder-owned or DI-managed state over process-wide mutable `static` state
- keep `Registry` as a first-class builder concept
- keep `TypeResolver` DI-managed instead of reviving `TypeRegistry`
- treat ambient APIs (`Mapper`, `IdGenerator`, `EventPublisher`) as convenience layers with explicit alternatives available

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
      add-jwt-auth.md
      add-rabbitmq-messaging.md
      add-redis-caching.md
      add-request-handler.md
      add-state-machine.md
  skills/
    nof-app-development/
      SKILL.md
      references/
        infrastructure.md
        recipes.md
```

## Which File To Use

| Scenario | Read Rules | Use Workflows | Use Skills |
|---|---|---|---|
| Contributing to NOF framework (`src/`, `tests/`, CI/CD) | `rules/nof-dev.md` | `workflows/nof-dev/*` | N/A |
| Building an app with NOF | `rules/app-dev.md` | `workflows/app-dev/*` | `skills/nof-app-development/SKILL.md` |

## Test Layout Notes

- Most package and host tests live under `tests/NOF.*.Tests`.
- Extension package tests are under `tests/Extensions/*`.
- Infrastructure provider tests are under `tests/Infrastructures/*`.
- Shared test helpers live under `tests/Common/*`.
- Source generator tests are colocated with their parent package test projects when practical.
