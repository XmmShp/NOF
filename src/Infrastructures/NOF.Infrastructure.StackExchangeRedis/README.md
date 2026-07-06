# NOF.Infrastructure.StackExchangeRedis

Redis caching infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides Redis-backed infrastructure for NOF using StackExchange.Redis, including `ICacheService` and `IBackplane` implementations.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args);

builder.Services.AddRedisCache(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));

builder.Services.AddRedisBackplane(builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' not found."));
```

Redis connection settings are typically resolved from your application configuration using the `redis` connection string.

## Dependencies

- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`StackExchange.Redis`](https://www.nuget.org/packages/StackExchange.Redis)

## Installation

```shell
dotnet add package NOF.Infrastructure.StackExchangeRedis
```

## License

Apache-2.0
