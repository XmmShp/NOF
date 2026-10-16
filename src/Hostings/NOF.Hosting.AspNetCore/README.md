# NOF.Hosting.AspNetCore

ASP.NET Core hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the ASP.NET Core host integration for NOF applications, including HTTP endpoint mapping from registered RPC servers, OpenAPI service registration, JSON serialization configuration, middleware pipeline, .NET 10 SSE streaming integration, and a NOF-oriented `IHostApplicationBuilder` implementation for web applications.

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
// Create() automatically applies infrastructure defaults and configures JSON options and OpenAPI services.
var builder = NOFWebApplicationBuilder.Create(args);

builder.AddRpcServer<MyAppService>();

var app = await builder.BuildAsync();

await app.RunAsync();
```

`Create()` already adds the calling assembly as an application part. Use `AddApplicationPart(...)` only when you need to register additional assemblies.

`AddRpcServer<TRpcServer>()` registers the RPC server and ASP.NET Core maps its HTTP endpoints automatically when the app initializes. Transport style and route prefix come from the RPC contract.

An RPC service contract can declare its intended HTTP transport style:

```csharp
[TransportOverHttp(HttpRpcStyle.JsonRpc, "/rpc")]
public interface IOrderService : IRpcService;
```

`TransportOverAttribute` is abstract and defined in `NOF.Contract`, allowing callers and implementations to discover the same transport choice from the service interface. The RPC service analyzer ensures that transport attributes are only placed on interfaces inheriting `IRpcService` and that each contract declares exactly one transport. HTTP route prefixes must be application-relative paths; a leading `/` is optional, while absolute URIs, route parameters, query strings, fragments, empty path segments, and path traversal segments are rejected. `AddRpcServer<TRpcServer>()` selects the endpoint mapper from `HttpRpcStyle`: `ControllerRpc` prepends the optional prefix to operation routes, and `JsonRpc` maps one JSON-RPC endpoint at that route. When a JSON-RPC prefix is omitted, the endpoint is mapped at `/`.

`[TransportOverMemory]` contracts are ignored by the ASP.NET Core transport and are never mapped to endpoints. They remain callable through their generated local clients inside the host process and must not declare `[HttpEndpoint]`.

ASP.NET Core endpoint mapping is implemented as an `IRpcServerTransport` registered in DI. The framework-level initialization step lives in `NOF.Infrastructure`; this package owns only the HTTP-specific contract inspection and endpoint mapping behavior.

Example request:

```json
{
  "jsonrpc": "2.0",
  "method": "GetOrder",
  "params": {
    "id": "0198e5dc-1f6d-7ea0-9bee-a7f4d65f3251"
  },
  "id": "75c966fa798d46829bb3fdf94a80ace8"
}
```

Each registered JSON-RPC server is mapped once, so the JSON-RPC method is only the contract operation name. The endpoint runs the same request inbound middleware and handler pipeline as normal HTTP RPC endpoints.

Methods on a JSON-RPC contract must not declare `[HttpEndpoint]`; that attribute is reserved for `ControllerRpc`, where it selects the HTTP verb and operation route.

The JSON-RPC endpoint supports unary and streaming calls with object `params`. Streaming responses use `text/event-stream`; each SSE `data` payload is a JSON-RPC response envelope containing one stream item and the original request ID. Batch requests and notifications are not supported.

Methods on ControllerRpc services are turned into minimal API endpoints:

```csharp
[TransportOverHttp(HttpRpcStyle.ControllerRpc)]
public interface IOrderService : IRpcService
{
    [HttpEndpoint(HttpVerb.Get, "/api/orders/get")]
    Result<OrderDto> Get(GetOrderRequest request);

    [HttpEndpoint(HttpVerb.Get, "/api/orders/watch")]
    StreamingResult<OrderEvent> Watch(WatchOrdersRequest request);
}
```

Route parameters such as `"{id}"` are not supported for RPC HTTP endpoints. Put input data on the request object instead.

For example, a contract prefix `/api/orders` and operation route `/get` produce
`/api/orders/get?Id=...` for GET. GET and DELETE bind request properties from query;
POST, PUT and PATCH bind the request from a JSON body. OpenAPI uses these fixed paths.
Generated clients use relative URIs; end a reverse-proxy `BaseAddress` path with `/`.

## HTTP failures and Result

Controller RPC keeps the HTTP transport outcome separate from the business Result:

| Outcome | Default HTTP status | Body / client behavior |
| --- | --- | --- |
| Authentication required but missing/invalid | 401 | Failed Result with Bearer challenge |
| Permission or configured tenant-policy denial | 403 | Failed Result with Bearer challenge |
| Resource absent, validation failure or conflict returned by a handler | 200 | Failed Result with the application's error code |
| `DomainException` from the handler | 200 | Failed Result preserving the domain code and message |
| Unhandled concurrency or transient exception inside the inbound handler pipeline | 200 | Failed Result with code `500`; handle known failures explicitly in the application |
| Malformed HTTP request/model binding failure | Usually 400 | ASP.NET Core binding response |
| Proxy, network or failure outside the handler pipeline | Host/transport dependent | May return non-2xx or throw a network/serialization exception |

Controller endpoints declare 200, 401 and 403 response metadata for OpenAPI. Business
error codes do not automatically become HTTP status codes. Stream authorization failures
return JSON with the authorization status before SSE starts; successful streams use SSE.
Once a stream has started, its HTTP status cannot be changed to report a later failure.

Generated clients deserialize a 2xx body into its Result, preserving `IsSuccess=false`.
Always inspect `IsSuccess` before using `Value`. Non-2xx becomes a failed Result whose
error code is the HTTP status and message contains the response body. The generated
client does not retry automatically. If the Host adds HTTP resilience, retry writes
only with a stable operation key and server-side deduplication. Transactional-message
background retries are independent of HTTP client retries.

JSON-RPC uses its own response/error envelope and should not be interpreted through
this Controller RPC mapping table.

For streaming methods, NOF emits `text/event-stream` responses and serializes each item from `StreamingResult<T>.Value` as an SSE `data:` payload. Generated HTTP clients automatically request SSE and materialize the response back into `Task<StreamingResult<T>>`.

## Installation

```shell
dotnet add package NOF.Hosting.AspNetCore
```

## License

Apache-2.0
