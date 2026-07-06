# NOF.Hosting.AspNetCore

ASP.NET Core hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the ASP.NET Core host integration for NOF applications, including HTTP endpoint mapping from registered RPC servers, OpenAPI service registration, JSON serialization configuration, middleware pipeline, .NET 10 SSE streaming integration, and the `INOFAppBuilder` implementation for web applications.

## Features

- **Automatic RPC Endpoint Mapping** - registered RPC servers are mapped to minimal API endpoints during application initialization
- **OpenAPI Registration** - built-in OpenAPI service registration; endpoint mapping stays explicit in the host application
- **Infrastructure Defaults** - `Create()` applies `NOF.Infrastructure` defaults for typical server-side hosts
- **JSON Configuration** - pre-configured `System.Text.Json` options with sensible defaults
- **Streaming RPC over SSE** - `StreamingResult<T>` endpoints are exposed as server-sent events via ASP.NET Core's .NET 10 SSE support
- **Invocation Context Middleware** - propagates tenant ID and other context through the request pipeline
- **`[AutoInject]` Support** - bundled source generators for automatic DI registration

## Usage

```csharp
// Create() automatically applies infrastructure defaults and configures JSON options, CORS, and OpenAPI services.
var builder = NOFWebApplicationBuilder.Create(args);

builder.AddRpcServer<MyAppService>();

var app = await builder.BuildAsync();

await app.RunAsync();
```

`Create()` already adds the calling assembly as an application part. Use `AddApplicationPart(...)` only when you need to register additional assemblies.

`AddRpcServer<TRpcServer>()` registers the RPC server and ASP.NET Core maps its HTTP endpoints automatically when the app initializes. `MapHttpEndpoint<TRpcServer>()` remains available when you want to map a server explicitly or apply a route prefix manually.

Methods on explicitly mapped RPC services are turned into minimal API endpoints:

```csharp
public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/orders/get")]
    Result<OrderDto> Get(GetOrderRequest request);

    [HttpEndpoint(HttpVerb.Get, "/api/orders/watch")]
    StreamingResult<OrderEvent> Watch(WatchOrdersRequest request);
}
```

Route parameters such as `"{id}"` are not supported for RPC HTTP endpoints. Put input data on the request object instead.

For streaming methods, NOF emits `text/event-stream` responses and serializes each item from `StreamingResult<T>.Value` as an SSE `data:` payload. Generated HTTP clients automatically request SSE and materialize the response back into `Task<StreamingResult<T>>`.

## Installation

```shell
dotnet add package NOF.Hosting.AspNetCore
```

## License

Apache-2.0
