# The Step Pipeline: From Reflection to CRTP

## What Steps Are

NOF applications are assembled from small, ordered units of work called **steps**. A step is either a *service registration step* — something that runs while the DI container is being built — or an *application initialization step* — something that runs after the host is constructed but before it starts.

The framework's job is to execute these steps in the right order. If step B depends on step A, A must run first. If step C needs to run before step D, that relationship must be declared and honored.

This is the step pipeline: a directed acyclic graph of configuration units, topologically sorted and executed in sequence.

## The Original Design

The first version was straightforward. `IServiceRegistrationStep` and `IApplicationInitializationStep` both extended a marker interface `IStep`. Ordering was declared with `IAfter<T>` and `IBefore<T>`:

```csharp
public interface IStep;
public interface IAfter<TDependency> where TDependency : IStep;
public interface IBefore<TDependency> where TDependency : IStep;
```

The `ConfiguratorGraph<T>` class took a collection of steps and built a dependency graph. For each step, it called `node.GetType()` to get the runtime type, then `type.GetInterfaces()` to find which `IAfter<>` and `IBefore<>` interfaces the step implemented. Those interfaces named the dependency targets; the graph resolver matched them to concrete steps in the collection.

It worked well enough for JIT-compiled applications. But when we turned our attention to Native AOT, the cracks appeared.

## The AOT Problem

The IL trimmer is aggressive. If it cannot statically prove that a type's interfaces will be needed at runtime, it strips them. `object.GetType()` returns a `Type` at runtime, but the trimmer has no annotation on `GetType()` that says "I need this type's interface metadata preserved." The result: `type.GetInterfaces()` returns an incomplete array — or an empty one — and the dependency graph silently loses edges.

The .NET annotation system provides `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` to tell the trimmer "preserve interface metadata for this type." But you can't annotate the return value of `object.GetType()` — it's a runtime method on `System.Object`.

Our initial workaround was honest but unsatisfying:

```csharp
[UnconditionalSuppressMessage("AOT", "IL2070",
    Justification = "Concrete step types are registered via DI and always preserved.")]
private static Type[] GetInterfaces(Type type) => type.GetInterfaces();
```

The suppression said: "Trust us, the types are always present because they're registered in DI." That was true in practice but invisible to the trimmer. It was a hole in our AOT story — the kind of hole that works until it doesn't.

## The Key Insight

The problem reduced to one question: *how do we get an annotated `Type` from a step instance?*

`object.GetType()` is unannotated and we can't change it. But we *can* add our own property. If `IStep` declares a `Type Type { get; }` property annotated with `[DynamicallyAccessedMembers(Interfaces)]`, then calling `step.Type.GetInterfaces()` is trim-safe — the trimmer sees the annotation and preserves the interface metadata.

The question then becomes: who provides the implementation?

We could require every concrete step to write:

```csharp
public Type Type => typeof(MyConcreteStep);
```

That works, but it's boilerplate. Every step author has to remember it. Forget it, and the dependency graph silently breaks. That's exactly the kind of implicit contract we try to avoid.

## Curiously Recurring Template Pattern (CRTP)

C# default interface methods (DIMs) let an interface provide a method body. Combined with a self-referential generic parameter, we can make the interface supply the correct `Type` automatically:

```csharp
public interface IStep
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
    Type Type { get; }
}

public interface IStep<TSelf> : IStep
    where TSelf : IStep<TSelf>
{
    Type IStep.Type => typeof(TSelf);
}
```

When a concrete class implements `IStep<MyStep>`, the DIM returns `typeof(MyStep)` — fully annotated, trim-safe, and zero-boilerplate for the step author.

This is the Curiously Recurring Template Pattern (CRTP). The type parameter `TSelf` is constrained to implement the interface it's parameterizing. The pattern is well-established in C++ and increasingly idiomatic in modern C# (see `INumber<TSelf>` in .NET 7+).

## The Three-Tier Hierarchy

One wrinkle: `IAfter<T>` and `IBefore<T>` constrain `T : IStep`. Intermediate interfaces like `IBaseSettingsServiceRegistrationStep` are used as `IAfter<>` targets — other steps declare `IAfter<IBaseSettingsServiceRegistrationStep>` to run after all base-settings steps. These intermediate interfaces can't be generic (you can't write `IAfter<IBaseSettingsServiceRegistrationStep<???>>`).

The solution is a three-tier hierarchy for each step family:

1. **Non-generic base** — the marker interface, used as `IAfter<>`/`IBefore<>` target
2. **`<TSelf>` CRTP variant** — adds `IStep<TSelf>`, provides the `Type` DIM
3. **`<TSelf, TMiddleware>` variant** (middleware only) — also infers `MiddlewareType` from the generic parameter

For example:

```csharp
// Tier 1: non-generic marker (IAfter<> target)
public interface IBaseSettingsServiceRegistrationStep : IServiceRegistrationStep;

// Tier 2: CRTP (provides IStep.Type)
public interface IBaseSettingsServiceRegistrationStep<TSelf>
    : IBaseSettingsServiceRegistrationStep, IStep<TSelf>
    where TSelf : IBaseSettingsServiceRegistrationStep<TSelf>;
```

Concrete steps implement the CRTP variant:

```csharp
public class CoreServicesRegistrationStep
    : IBaseSettingsServiceRegistrationStep<CoreServicesRegistrationStep>
{
    // IStep.Type is automatically typeof(CoreServicesRegistrationStep)
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder) { ... }
}
```

Middleware steps use the three-parameter variant:

```csharp
public class ExceptionInboundMiddlewareStep
    : IInboundMiddlewareStep<ExceptionInboundMiddlewareStep, ExceptionInboundMiddleware>;
// IStep.Type => typeof(ExceptionInboundMiddlewareStep)
// MiddlewareType => typeof(ExceptionInboundMiddleware)
```

## The ConfiguratorGraph Fix

With the `Type` property in place, `ConfiguratorGraph` becomes trim-safe without any suppression:

```csharp
private void IndexNode(T node)
{
    // node.Type carries [DynamicallyAccessedMembers(Interfaces)] via CRTP
    var type = node.Type;
    var ancestors = new HashSet<Type>();
    for (var t = type; t is not null; t = t.BaseType)
        ancestors.Add(t);
    foreach (var iface in type.GetInterfaces())  // trim-safe!
        ancestors.Add(iface);
    // ...
}
```

The `[UnconditionalSuppressMessage]` and the `GetInterfaces` wrapper method are gone. The trimmer can statically verify that every `Type` flowing into `GetInterfaces()` is annotated.

## Self-Referential Constraints

Each CRTP variant carries a `where TSelf : IXxxStep<TSelf>` constraint. This serves two purposes:

1. **Correctness** — prevents `class Foo : IServiceRegistrationStep<Bar>` (mismatched `TSelf`)
2. **Constraint propagation** — satisfies `IStep<TSelf>`'s own `where TSelf : IStep<TSelf>` requirement

The constraints cascade: `IBaseSettingsServiceRegistrationStep<TSelf> where TSelf : IBaseSettingsServiceRegistrationStep<TSelf>` implies `TSelf : IStep<TSelf>` because `IBaseSettingsServiceRegistrationStep<TSelf> : IStep<TSelf>`.

## Lambda Wrappers

Two utility classes — `ServiceRegistrationStep` and `ApplicationInitializationStep` — accept lambdas for ad-hoc steps added via `builder.AddRegistrationStep(ctx => ...)`. Since they don't have a meaningful `TSelf`, they handle `Type` differently:

- `ServiceRegistrationStep` uses CRTP with itself: `ServiceRegistrationStep : IServiceRegistrationStep<ServiceRegistrationStep>`
- `ApplicationInitializationStep` implements `IStep.Type` directly as `typeof(ApplicationInitializationStep)`

Both are valid — the dependency graph only needs a correct, annotated `Type`. Lambda steps rarely participate in `IAfter<>`/`IBefore<>` ordering anyway.

## Summary

| Before | After |
|--------|-------|
| `node.GetType()` (unannotated) | `node.Type` (annotated via CRTP DIM) |
| `[UnconditionalSuppressMessage]` on `GetInterfaces` | No suppression needed |
| Concrete steps: just implement `IServiceRegistrationStep` | Concrete steps: implement `IServiceRegistrationStep<TSelf>` |
| Implicit trust that DI preserves types | Explicit annotation that trimmer can verify |

The cost is one extra generic parameter on the step interface. The benefit is a fully verifiable AOT story with zero suppressions in the dependency graph — the part of the framework that touches every step in every application.
