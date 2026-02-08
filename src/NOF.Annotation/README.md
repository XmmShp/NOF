# NOF.Annotation

Shared attribute definitions for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

This package provides marker attributes and enums used across all NOF layers. It has **zero dependencies** and is designed to be referenced by any project in your solution without pulling in framework internals.

## Contents

### `[AutoInject]` Attribute

Marks a class for automatic DI container registration via source generation.

```csharp
[AutoInject(Lifetime.Scoped)]
public class OrderService : IOrderService
{
    // Automatically registered as IOrderService in the DI container
}

[AutoInject(Lifetime.Singleton, RegisterTypes = [typeof(ICacheService)])]
public class RedisCacheService : ICacheService, IDisposable
{
    // Registered only as ICacheService (not IDisposable)
}
```

### `Lifetime` Enum

Defines the service lifetime for `[AutoInject]` registrations: `Singleton`, `Scoped`, or `Transient`.

## Installation

```shell
dotnet add package NOF.Annotation
```

## License

Apache-2.0
