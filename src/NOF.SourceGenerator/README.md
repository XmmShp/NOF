# NOF.SourceGenerator

Roslyn source generator package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides compile-time code generation for the `[AutoInject]` attribute. When a class is annotated with `[AutoInject]`, this generator automatically emits DI registration code, eliminating the need for manual `services.AddScoped<>()` calls.

## How It Works

This is a **development dependency** — it runs at compile time and produces no runtime assembly. It is automatically included when you reference `NOF.Hosting.AspNetCore`.

### Generated Registration

```csharp
// Your code
[AutoInject(Lifetime.Scoped)]
public class OrderService : IOrderService { }

// Generated at compile time — registers OrderService as IOrderService (Scoped)
```

The generator scans for all `[AutoInject]` usages in the project and emits a single registration extension method that is called during application startup.

## Installation

This package is typically consumed transitively via `NOF.Hosting.AspNetCore`. If you need it standalone:

```shell
dotnet add package NOF.SourceGenerator
```

## License

Apache-2.0
