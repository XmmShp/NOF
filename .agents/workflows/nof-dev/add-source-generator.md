---
description: How to add or modify a Roslyn source generator in the NOF framework
---

# Add or Modify a Source Generator

NOF uses Roslyn incremental source generators for compile-time code generation.

## Creating a New Generator

1. Create the project under `src/NOF.<Layer>.SourceGenerator/` (e.g., `NOF.Contract.SourceGenerator`).

2. Set up the `.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>netstandard2.0</TargetFramework>
       <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
       <IsRoslynComponent>true</IsRoslynComponent>
       <IsPackable>false</IsPackable>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
       <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" />
     </ItemGroup>
   </Project>
   ```

3. Implement `IIncrementalGenerator`:
   ```csharp
   [Generator]
   public class MyGenerator : IIncrementalGenerator
   {
       public void Initialize(IncrementalGeneratorInitializationContext context)
       {
           // Register syntax/symbol providers and output source
       }
   }
   ```

4. Reference the generator from the consuming project as an analyzer:
   ```xml
   <ProjectReference Include="..\NOF.<Layer>.SourceGenerator\NOF.<Layer>.SourceGenerator.csproj"
                     OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
   ```

5. Add the project to `NOF.slnx`.

## Testing Source Generators

1. Add test cases in `tests/NOF.SourceGenerator.Tests/`.
2. Use `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` for verification.
3. Follow existing test patterns — see `ExposeToHttpEndpointMapperTests.cs` or `AutoInjectGeneratorTests.cs`.

// turbo
4. Run tests: `dotnet test tests/NOF.SourceGenerator.Tests/`

> **Reminder**: See the complete change checklist in `rules/nof-dev.md` — don't forget CI/CD, docs, sample, and tests.
