---
description: Register and order a post-build NOF application initialization step with IApplicationInitializationStep
---

# Add an Application Initialization Step

NOF currently has one step contract: `IApplicationInitializationStep`. Service registration happens directly against `IServiceCollection` while configuring the builder.

## 1. Implement the Step

```csharp
using Microsoft.Extensions.Hosting;
using NOF.Hosting;

public sealed class MyFeatureInitializationStep : IApplicationInitializationStep
{
    public TopologyComparison Compare(IApplicationInitializationStep other)
        => other is DatabaseReadyInitializationStep
            ? TopologyComparison.After
            : TopologyComparison.DoesNotMatter;

    public async Task ExecuteAsync(IHost app)
    {
        var service = app.Services.GetRequiredService<IMyService>();
        await service.InitializeAsync();
    }
}
```

`Compare(other)` returns:

- `Before` when this instance must run before `other`
- `After` when this instance must run after `other`
- `DoesNotMatter` when there is no ordering edge

Ordering is instance/type based; there are no `IAfter<T>` or `IBefore<T>` marker interfaces.

## 2. Register the Step

```csharp
builder.Services.TryAddInitializationStep<MyFeatureInitializationStep>();
```

Available APIs include instance, type, and factory overloads of `AddInitializationStep(...)` and `TryAddInitializationStep(...)`, plus `RemoveInitializationStep<T>()` and the predicate overload for registered instances.

Use `TryAddInitializationStep` when only one implementation-type registration should exist. Use `AddInitializationStep` when multiple instances of the same type are intentional.

## 3. Understand Execution

`BuildNOFAsync(...)` builds the host and then calls `InitializeNOFAsync()`. That method resolves all registered initialization steps, orders them with `DependencyGraph<IApplicationInitializationStep>`, and executes them sequentially. A dependency cycle throws `InvalidOperationException`.

The same `ITopologizable<T>` / `TopologyComparison` model orders request/message middleware and RPC transports, so follow the same comparison semantics when extending those pipelines.

## 4. Test

Test registration idempotency separately from ordering. For ordering tests, register concrete probe instances and assert execution order; also cover any replacement/removal behavior exposed by the feature.
