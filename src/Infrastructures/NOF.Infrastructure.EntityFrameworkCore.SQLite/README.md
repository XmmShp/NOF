# NOF.Infrastructure.EntityFrameworkCore.SQLite

SQLite provider package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Extends `NOF.Infrastructure.EntityFrameworkCore` with SQLite-specific database provider configuration via Microsoft.Data.Sqlite. This package wires up the `IDbContextConfigurator` for SQLite and provides extension methods for the NOF selector.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.AddEFCore<AppDbContext>()
    .AutoMigrate()
    .UseSqlite();
```

`UseSqlite()` configures the `NOFDbContext` to use SQLite with the connection string resolved from your application configuration (default connection name: `"sqlite"`).

## Dependencies

- `NOF.Infrastructure` (contains the EF Core integration)
- [`Microsoft.EntityFrameworkCore.Sqlite`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite)

## Installation

```shell
dotnet add package NOF.Infrastructure.EntityFrameworkCore.SQLite
```

## In-Memory SQLite

For tests or lightweight local scenarios, you can use SQLite's in-memory mode while
still keeping relational behavior:

```csharp
builder.AddEFCore<AppDbContext>()
    .UseSingleTenant()
    .UseSqliteInMemory();
```

`UseSqliteInMemory()` keeps a named in-memory database alive across `DbContext`
instances by holding an internal shared connection open for the process lifetime.

## License

Apache-2.0
