# .agents — AI Agent Instructions for NOF

> **Audience**: Both human developers and AI coding assistants (Copilot, Windsurf Cascade, Cursor, Cline, etc.)

This directory contains structured instructions that help AI agents work effectively with the NOF codebase. Human developers should also read these files to understand the conventions and workflows.

## Directory Structure

```
.agents/
  rules/
    nof-dev.md        — Rules for developing the NOF framework itself (src/, tests/, CI/CD)
    app-dev.md        — Rules for developing applications that USE the NOF framework
  workflows/
    nof-dev/          — Step-by-step workflows for NOF framework contributors
      add-infrastructure-package.md
      add-source-generator.md
      add-step.md
      run-ci-locally.md
    app-dev/          — Step-by-step workflows for NOF application developers
      add-domain-entity.md
      add-domain-event-handler.md
      add-efcore-database.md
      add-handler.md
      add-jwt-auth.md
      add-masstransit-messaging.md
      add-redis-caching.md
      add-request-handler.md
      add-state-machine.md
      scaffold-nof-app.md
  skills/
    nof-app-development.md — Comprehensive skill for AI agents building NOF applications
```

## Who Should Use What

| You are… | Read rules | Use workflows | Use skills |
|----------|-----------|---------------|------------|
| **Contributing to NOF** (framework code, tests, CI/CD) | `rules/nof-dev.md` | `workflows/nof-dev/*` | — |
| **Building an app with NOF** (your own project) | `rules/app-dev.md` | `workflows/app-dev/*` | `skills/nof-app-development.md` |

## For AI Agent Clients

The `.agents/` directory is **checked into Git** so all contributors share the same instructions. However, each AI client (Windsurf, Cursor, Copilot, etc.) has its own configuration directory that is **gitignored** (see `.gitignore`).

To share these instructions with your AI client, create a **symlink** from your client's config directory to the relevant files here. See the comments in `.gitignore` for details.
