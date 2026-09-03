---
description: Add or modify a NOF Roslyn incremental generator/analyzer without depending on other generated output
---

# Add or Modify a Source Generator

## 1. Place the Generator with Its Runtime Package

- Core generators use projects such as `src/NOF.Application.SourceGenerator/`.
- Provider-specific generators may live beside their provider, such as `src/Infrastructures/NOF.Infrastructure.EntityFrameworkCore.SourceGenerator/`.
- Target `netstandard2.0`, set `IsRoslynComponent`, `DevelopmentDependency`, and `IsPackable` consistently with existing generator projects, and keep package versions centralized.

## 2. Preserve Generator Isolation

A generator may inspect only user-authored declarations and stable types from referenced runtime assemblies. It must not require another generator's emitted symbols, members, attributes, interfaces, or base types to appear in Roslyn input.

Generated names and signatures may reference other generated artifacts only when both sides derive them independently from the same user-authored declaration. This is how RPC contract, HTTP client, server, auto-inject, and local-client generators remain order-independent.

## 3. Wire the Parent Package

Follow the owning runtime project:

```xml
<ProjectReference Include="..\NOF.MyFeature.SourceGenerator\NOF.MyFeature.SourceGenerator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />

<None Include="..\NOF.MyFeature.SourceGenerator\bin\$(Configuration)\netstandard2.0\NOF.MyFeature.SourceGenerator.dll"
      Pack="true"
      PackagePath="analyzers/dotnet/cs"
      Visible="false" />
```

Keep the runtime package as the public NuGet boundary; generator projects are non-packable.

## 4. Add Tests to the Owning Test Project

- `NOF.Abstraction.SourceGenerator` -> `tests/NOF.Abstraction.Tests`
- `NOF.Domain.SourceGenerator` -> `tests/NOF.Domain.Tests`
- `NOF.Contract.SourceGenerator` -> `tests/NOF.Contract.Tests`
- `NOF.Application.SourceGenerator` -> `tests/NOF.Application.Tests`
- `NOF.Hosting.SourceGenerator` -> `tests/NOF.Hosting.Tests`
- `NOF.Infrastructure.SourceGenerator` and the EF Core provider analyzers -> `tests/NOF.Infrastructure.Tests`

Reuse `tests/Common/SourceGenerator/*`. Reference the generator project normally from the test project so tests can instantiate it.

Test diagnostics and generated source, including duplicate inputs, generic/nested types, nullable behavior, and registration idempotency as applicable. For cooperating generators, run them together in normal and reversed order.

## 5. Validate

```bash
dotnet test tests/NOF.Application.Tests/NOF.Application.Tests.csproj
dotnet format --verify-no-changes --verbosity diagnostic
dotnet build NOF.slnx --configuration Release
```

Replace the first command with the owning test project. Review AOT/trimming annotations and package analyzer inclusion when public generated runtime paths change.
