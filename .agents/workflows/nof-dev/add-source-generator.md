---
description: Add or modify a Roslyn source generator in NOF
---

# Add or Modify a Source Generator

NOF uses incremental generators for compile-time code generation.

## 1. Create or Update Generator Project

- Place generator projects under `src/*SourceGenerator/`.
- Keep target/framework settings aligned with existing generator projects.
- Reference shared helper code from `src/Common/NOF.SourceGenerator.Shared/` when applicable.

## 2. Wire Generator to Parent Package

- Add generator project as analyzer reference from its parent package project.
- Ensure the parent package remains the public package boundary.

## 3. Add Tests

- Put generator tests into the parent package test project (do not create a generic `NOF.SourceGenerator.Tests` bucket).
- Examples:
- `NOF.Domain` + `NOF.Domain.SourceGenerator` -> `tests/NOF.Domain.Tests`
- `NOF.Contract` + `NOF.Contract.SourceGenerator` -> `tests/NOF.Contract.Tests`
- `NOF.Application` + `NOF.Application.SourceGenerator` -> `tests/NOF.Application.Tests`

## 4. Validate

```bash
dotnet test tests/NOF.Domain.Tests
dotnet test tests/NOF.Contract.Tests
dotnet test tests/NOF.Application.Tests
```

Adjust commands to the package you changed.
