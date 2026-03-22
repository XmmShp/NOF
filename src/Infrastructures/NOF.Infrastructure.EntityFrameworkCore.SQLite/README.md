# NOF.Infrastructure.EntityFrameworkCore.SQLite

SQLite provider package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Extends `NOF.Infrastructure.EntityFrameworkCore` with SQLite-specific database provider configuration via Microsoft.Data.Sqlite. This package wires up the `IDbContextConfigurator` for SQLite and provides extension methods for the NOF selector.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddEFCore<AppDbContext>()
    .AutoMigrate()
    .UseSqlite();
```

`UseSqlite()` configures the `NOFDbContext` to use SQLite with the connection string resolved from your application configuration (default connection name: `"sqlite"`).

## Dependencies

- [`NOF.Infrastructure.EntityFrameworkCore`](https://www.nuget.org/packages/NOF.Infrastructure.EntityFrameworkCore)
- [`Microsoft.EntityFrameworkCore.Sqlite`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite)

## Installation

```shell
dotnet add package NOF.Infrastructure.EntityFrameworkCore.SQLite
```

## License

Apache-2.0

