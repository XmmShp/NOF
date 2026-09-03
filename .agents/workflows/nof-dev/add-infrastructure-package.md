---
description: Add a provider package under src/Infrastructures using the current IServiceCollection and host-builder extension model
---

# Add an Infrastructure Provider Package

## 1. Create the Project

Place the project at `src/Infrastructures/NOF.Infrastructure.<Name>/NOF.Infrastructure.<Name>.csproj`.

Use the existing providers as the template. A minimal provider generally looks like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAotCompatible>true</IsAotCompatible>
    <PackageId>NOF.Infrastructure.MyProvider</PackageId>
    <Description>MyProvider integration for the NOF Framework.</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\NOF.Infrastructure\NOF.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

Add `NOF.Application` or `NOF.Hosting` references only when the implementation uses their APIs. Add external dependency versions to root `Directory.Packages.props`, never to the project file.

## 2. Register Services Directly

There is no service-registration-step pipeline. Expose an `IServiceCollection` extension for runtime replacement/configuration, following RabbitMQ and StackExchange.Redis:

```csharp
namespace NOF.Hosting;

public static partial class NOFInfrastructureMyProviderExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMyProvider(Action<MyProviderOptions> configure)
        {
            services.Configure(configure);
            services.ReplaceOrAddSingleton<IMyRuntime, MyRuntime>();
            return services;
        }
    }
}
```

Use an `IHostApplicationBuilder` extension when registration needs environment data or returns a fluent selector, as the EF Core and NHibernate providers do.

Use `ReplaceOrAdd*` when the provider intentionally replaces a NOF default. Use `TryAdd*` / `TryAddEnumerable` when user overrides or multiple implementations must be preserved.

## 3. Add Initialization Only When Necessary

If the provider must act after host construction, implement `IApplicationInitializationStep`, define ordering with `Compare(...)`, and register it through `services.AddInitializationStep(...)` or `TryAddInitializationStep(...)`. Do not recreate `IServiceRegistrationStep`, `IAfter<T>`, or `IBefore<T>`.

## 4. Wire Repository Metadata

1. Add the project to `NOF.slnx` under `/src/Infrastructures/`.
2. Add a package `README.md` with installation, registration, and replacement semantics.
3. Add the package to the root `README.md` table.
4. If a dedicated test project is warranted, place it under `tests/Infrastructures/NOF.Infrastructure.<Name>.Tests/` and add it to `NOF.slnx`.
5. Keep `PackageId` present. CD discovers packable `src/**/*.csproj` projects with a `PackageId`; it skips projects explicitly marked `<IsPackable>false</IsPackable>`.

## 5. Verify

Run formatting, the provider tests, a Release build of the package, and the sample or integration tests that exercise replacement of the default runtime service.
