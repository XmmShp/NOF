## Description

<!-- What does this PR do? Why is it needed? -->

## Related Issues

<!-- Link related issues: Closes #123, Fixes #456 -->

## Type of Change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to change)
- [ ] Documentation update
- [ ] Refactoring (no functional changes)
- [ ] CI/CD or tooling change

## Checklist

<!-- For both human developers and AI agents: check ALL applicable items. Do NOT only update source code. -->

### Build & Quality

- [ ] Branch is up-to-date with `develop`
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet build --configuration Release` succeeds with no warnings
- [ ] `dotnet test` passes
- [ ] Commit messages follow [conventional commits](../CONTRIBUTING.md#commit-conventions)

### Code Completeness

- [ ] New public APIs have XML documentation
- [ ] Tests added/updated for behavioral changes
- [ ] Sample application updated if APIs changed (`sample/` must still compile)

### Documentation & Tooling

- [ ] `README.md` updated (if public API surface, packages, or architecture changed)
- [ ] `CONTRIBUTING.md` updated (if conventions or processes changed)
- [ ] `docs/` updated (if DocFX API docs affected)
- [ ] CI/CD workflows updated (`.github/workflows/` — if new packages or test projects added)
- [ ] AI agent instructions updated (`.agents/` rules/workflows/skills and `.github/copilot-instructions.md`)
