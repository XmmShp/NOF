# The Step Pipeline

## What Steps Are

NOF applications are assembled from small, ordered units of work called **steps**.
A step is either:

- a **service registration step** that runs while the DI container is being built
- an **application initialization step** that runs after the host is constructed

The framework executes these steps in dependency order.

## Core Contracts

```csharp
public interface IAfter<TDependency>;
public interface IBefore<TDependency>;

public interface IServiceRegistrationStep
{
    ValueTask ExecuteAsync(IServiceRegistrationContext builder);
}

public interface IApplicationInitializationStep
{
    Task ExecuteAsync(IHost app);
}
```

## Ordering Model

Ordering is declared through marker interfaces:

- `IAfter<T>` means the current step must run after `T`
- `IBefore<T>` means the current step must run before `T`

NOF builds a dependency graph from the registered step instances and executes them in topological order.

## Registration and Initialization

`NOFAppBuilder<T>` keeps two collections internally:

- service registration steps
- application initialization steps

During `BuildAsync()` it:

1. adds framework defaults such as auto-inject registration and hosting defaults
2. executes all service registration steps in dependency order
3. builds the host
4. executes all initialization steps in dependency order

## Practical Guidance

- Use a registration step when you need to add or configure services.
- Use an initialization step when you need the fully built host or endpoint pipeline.
- Use `TryAddRegistrationStep(...)` or `TryAddInitializationStep(...)` when duplicate registration should be ignored.
- Use `RemoveRegistrationStep(...)` or `RemoveInitializationStep(...)` when a host wants to replace a default behavior.
