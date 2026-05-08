# NOF.Infrastructure.StackExchangeRedis

Redis caching infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides a Redis-backed `ICacheService` implementation using StackExchange.Redis. Integrates with the NOF step pipeline for automatic service registration and configuration, and can also be consumed through `IDistributedCache`.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args);

builder.AddRedisCache(builder.Configuration.GetConnectionString("redis"));
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
