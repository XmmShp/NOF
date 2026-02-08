# NOF.Infrastructure.EntityFrameworkCore.PostgreSQL

PostgreSQL provider package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Extends `NOF.Infrastructure.EntityFrameworkCore` with PostgreSQL-specific database provider configuration via Npgsql. This package wires up the `IDbContextConfigurator` for PostgreSQL and provides extension methods for the NOF selector.

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddEFCore<AppDbContext>()
    .AutoMigrate()
    .UsePostgreSQL();
```

`UsePostgreSQL()` configures the `NOFDbContext` to use PostgreSQL with the connection string resolved from your application configuration (default connection name: `"postgres"`).

## Dependencies

- [`NOF.Infrastructure.EntityFrameworkCore`](https://www.nuget.org/packages/NOF.Infrastructure.EntityFrameworkCore)
- [`Npgsql.EntityFrameworkCore.PostgreSQL`](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL)

## Installation

```shell
dotnet add package NOF.Infrastructure.EntityFrameworkCore.PostgreSQL
```

## License

Apache-2.0
