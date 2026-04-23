# NOF.Hosting.AspNetCore

ASP.NET Core hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the ASP.NET Core host integration for NOF applications, including HTTP endpoint mapping from explicitly registered RPC servers, OpenAPI/Scalar documentation, JSON serialization configuration, middleware pipeline, and the `INOFAppBuilder` implementation for web applications.

## Features

- **Explicit Service Endpoint Mapping** - `MapHttpEndpoint<TRpcServer>()` maps RPC server handlers to minimal API endpoints
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

app.MapHttpEndpoint<MyAppService>();

await app.RunAsync();
```

Methods on explicitly mapped RPC services are turned into minimal API endpoints:

```csharp
public partial interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/orders/get")]
    Result<OrderDto> Get(GetOrderRequest request);
}
```

Route parameters such as `"{id}"` are not supported for RPC HTTP endpoints. Put input data on the request object instead.

## Installation

```shell
dotnet add package NOF.Hosting.AspNetCore
```

## License

Apache-2.0
