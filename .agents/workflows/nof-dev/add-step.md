---
description: How to add a new pipeline step (service registration or application initialization) to the NOF framework
---

# Add a New Pipeline Step

NOF uses a dependency-aware step pipeline for service registration and application initialization.

## Service Registration Step

Runs during DI container setup, before the host is built.

1. Create a class implementing `IServiceRegistrationStep`:
   ```csharp
   public sealed class MyFeatureRegistrationStep : IServiceRegistrationStep, IAfter<IBaseSettingsServiceRegistrationStep>
   {
       public ValueTask ExecuteAsync(IServiceRegistrationContext context)
       {
           context.Services.AddSingleton<IMyService, MyService>();
           return ValueTask.CompletedTask;
       }
   }
   ```

2. Use `IAfter<T>` to declare that this step must run after another step.
3. Use `IBefore<T>` to declare that this step must run before another step.
4. Prefer the predefined marker interfaces from `NOF.Infrastructure/Steps/PredefinedSteps.cs` when you want to plug into a stable slot such as base settings, dependent service registration, or endpoint initialization.
5. Register the step in the builder (typically via an extension method):
   ```csharp
   public static INOFAppBuilder AddMyFeature(this INOFAppBuilder builder)
   {
       builder.TryAddRegistrationStep(new MyFeatureRegistrationStep());
       return builder;
   }
   ```

## Application Initialization Step

Runs after the host is built but before it starts.

1. Create a class implementing `IApplicationInitializationStep`:
   ```csharp
   public sealed class MyFeatureInitializationStep : IApplicationInitializationStep, IAfter<IEndpointInitializationStep>
   {
       public async Task ExecuteAsync(IHost host)
       {
           var service = host.Services.GetRequiredService<IMyService>();
           await service.InitializeAsync();
       }
   }
   ```

2. Register via `builder.TryAddInitializationStep(new MyFeatureInitializationStep())`.

## Step Ordering

- Steps are executed in **topological order** based on `IAfter<T>` and `IBefore<T>` declarations.
- Circular dependencies will cause a runtime error.
- `NOFAppBuilder.BuildAsync()` always adds `AutoInjectServiceRegistrationStep` and hosting defaults before executing registered service steps.
- `NOF.Infrastructure` exposes stable predefined ordering slots:
  - `IBaseSettingsServiceRegistrationStep`
  - `IDependentServiceRegistrationStep`
  - `IDatabaseMigrationInitializationStep`
  - `IDataSeedInitializationStep`
  - `IObservabilityInitializationStep`
  - `ISecurityInitializationStep`
  - `IResponseFormattingInitializationStep`
  - `IAuthenticationInitializationStep`
  - `IBusinessLogicInitializationStep`
  - `IEndpointInitializationStep`
- Concrete defaults such as `OpenTelemetryRegistrationStep`, `RequestHandlerServiceRegistrationStep`, and `HandlerServiceRegistrationStep` come from `NOF.Infrastructure`.
- Ambient conveniences like `Mapper`, `IdGenerator`, and `EventPublisher` are no longer initialized through application initialization steps; they are activated in scoped execution boundaries through `IDaemonService`.

> **Reminder**: See the complete change checklist in `rules/nof-dev.md` — don't forget CI/CD, docs, sample, and tests.
