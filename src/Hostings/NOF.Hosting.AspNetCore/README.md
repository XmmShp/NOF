# NOF.Hosting.AspNetCore

ASP.NET Core hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the ASP.NET Core host integration for NOF applications, including source-generated HTTP endpoint mapping from explicitly registered RPC services, OpenAPI/Scalar documentation, JSON serialization configuration, middleware pipeline, and the `INOFAppBuilder` implementation for web applications.

## Features

- **Explicit Service Endpoint Mapping** - source generators turn `MapServiceToHttpEndpoints<TService>()` calls into minimal API endpoints
- **OpenAPI & Scalar** - built-in OpenAPI document generation with Scalar UI
- **JSON Configuration** - pre-configured `System.Text.Json` options with sensible defaults
- **Invocation Context Middleware** - propagates tenant ID and other context through the request pipeline
- **`[AutoInject]` Support** - bundled source generators for automatic DI registration

## Usage

```csharp
// useDefaults: true automatically calls UseDefaultSettings()
// which configures JSON options, CORS, and Scalar (in dev mode)
var builder = NOFWebApplicationBuilder.Create(args, useDefaults: true);

builder.WithAutoApplicationParts();

var app = await builder.BuildAsync();

app.MapServiceToHttpEndpoints<IMyAppService>();

await app.RunAsync();
```

Methods on explicitly mapped RPC services are turned into minimal API endpoints:

```csharp
public partial interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/orders/{id}")]
    Task<Result<OrderDto>> GetAsync(GetOrderRequest request);
}
```

## Installation

```shell
dotnet add package NOF.Hosting.AspNetCore
```

## License

Apache-2.0

