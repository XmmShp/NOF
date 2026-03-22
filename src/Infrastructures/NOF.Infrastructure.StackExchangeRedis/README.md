# NOF.Infrastructure.StackExchangeRedis

Redis caching infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides a Redis-backed `ICacheService` implementation using StackExchange.Redis. Integrates with the NOF step pipeline for automatic service registration and configuration, and can also be consumed through `IDistributedCache`.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddRedisCache();
```

Redis connection settings are resolved from your application configuration (default connection name: `"redis"`).

For Redis-specific data structure abstractions such as hashes, sets, lists, and sorted sets, reference `NOF.Application.Extension.Redis` and inject `IRedisCacheService`.

## Dependencies

- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`NOF.Application.Extension.Redis`](https://www.nuget.org/packages/NOF.Application.Extension.Redis)
- [`StackExchange.Redis`](https://www.nuget.org/packages/StackExchange.Redis)

## Installation

```shell
dotnet add package NOF.Infrastructure.StackExchangeRedis
```

## License

Apache-2.0

