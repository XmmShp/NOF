# NOF.Infrastructure.StackExchangeRedis

Redis caching infrastructure package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides a Redis-backed `ICacheService` implementation using StackExchange.Redis. Integrates with the NOF step pipeline for automatic service registration and configuration.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.Services.AddRedisCache();
```

Redis connection settings are resolved from your application configuration (default connection name: `"redis"`).

## Dependencies

- [`NOF.Infrastructure.Core`](https://www.nuget.org/packages/NOF.Infrastructure.Core)
- [`StackExchange.Redis`](https://www.nuget.org/packages/StackExchange.Redis)

## Installation

```shell
dotnet add package NOF.Infrastructure.StackExchangeRedis
```

## License

Apache-2.0
