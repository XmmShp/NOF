---
description: How to add a new infrastructure package to the NOF framework
---

# Add a New Infrastructure Package

Follow these steps to add a new infrastructure provider package to NOF.

1. Create the project directory under `src/Infrastructures/NOF.Infrastructure.<Name>/`.

2. Create the `.csproj` file with the following structure:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
       <Description>NOF infrastructure package for <Name>.</Description>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="..\..\NOF.Infrastructure.Core\NOF.Infrastructure.Core.csproj" />
     </ItemGroup>
   </Project>
   ```

3. Add any new NuGet dependency versions to the root `Directory.Packages.props` (never in the `.csproj`).

4. Create a `README.md` in the project directory with a brief description.

5. Implement a service registration step:
   - Create a class implementing `IServiceRegistrationStep`.
   - Use `IAfter<T>` / `IBefore<T>` to declare ordering dependencies.
   - Register services in `ExecuteAsync(IServiceRegistrationContext context)`.

6. Create a public extension method on `INOFAppBuilder` for fluent configuration (e.g., `builder.AddMyProvider()`).

7. Add the project to `NOF.slnx` under the `/src/Infrastructures/` folder.

8. Add `dotnet pack` commands for the new project in both `.github/workflows/cd.yml` and `.github/workflows/release.yml`.

9. Add the package to the table in `README.md`.

10. Add tests under `tests/` if applicable.

> **Reminder**: See the complete change checklist in `rules/nof-dev.md` — don't forget CI/CD, docs, sample, and tests.
