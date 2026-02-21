---
description: How to add a new pipeline step (service registration or application initialization) to the NOF framework
---

# Add a New Pipeline Step

NOF uses a dependency-aware step pipeline for service registration and application initialization.

## Service Registration Step

Runs during DI container setup, before the host is built.

1. Create a class implementing `IServiceRegistrationStep`:
   ```csharp
   public class MyFeatureRegistrationStep : IServiceRegistrationStep, IAfter<CoreServicesRegistrationStep>
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
4. Register the step in the builder (typically via an extension method):
   ```csharp
   public static INOFAppBuilder AddMyFeature(this INOFAppBuilder builder)
   {
       builder.AddRegistrationStep(new MyFeatureRegistrationStep());
       return builder;
   }
   ```

## Application Initialization Step

Runs after the host is built but before it starts.

1. Create a class implementing `IApplicationInitializationStep`:
   ```csharp
   public class MyFeatureInitializationStep : IApplicationInitializationStep
   {
       public async Task ExecuteAsync(IApplicationInitializationContext context, IHost host)
       {
           var service = host.Services.GetRequiredService<IMyService>();
           await service.InitializeAsync();
       }
   }
   ```

2. Register via `builder.AddInitializationStep(new MyFeatureInitializationStep())`.

## Step Ordering

- Steps are executed in **topological order** based on `IAfter<T>` and `IBefore<T>` declarations.
- Circular dependencies will cause a runtime error.
- Default steps registered by `NOFAppBuilder`:
  - `CoreServicesRegistrationStep` — core services
  - `CacheServiceRegistrationStep` — caching
  - `OutboxRegistrationStep` — transactional outbox
  - `OpenTelemetryRegistrationStep` — observability
  - `SnowflakeIdGeneratorRegistrationStep` — ID generation
  - Various inbound/outbound middleware steps

> **Reminder**: See the complete change checklist in `rules/nof-dev.md` — don't forget CI/CD, docs, sample, and tests.
