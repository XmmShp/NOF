# NOF.Application.Extension.Redis

Redis data-structure abstractions for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides `IRedisCacheService` and extension methods for common Redis data structures (hashes, sets, lists, sorted sets, pub/sub style patterns), built on top of NOF's cache abstractions.

Use this package when your application logic needs Redis-native operations beyond basic key/value caching.

## Usage

Register Redis cache infrastructure first, then inject Redis abstractions in your application services.

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddRedisCache();
```

After registration, consume Redis features through NOF application services (for example, via `IRedisCacheService`).

## Dependencies

- [`NOF.Application`](https://www.nuget.org/packages/NOF.Application)
- [`NOF.Contract`](https://www.nuget.org/packages/NOF.Contract)

## Installation

```shell
dotnet add package NOF.Application.Extension.Redis
```

## License

Apache-2.0

