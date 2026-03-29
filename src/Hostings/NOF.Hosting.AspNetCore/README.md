# NOF.Hosting.AspNetCore

ASP.NET Core hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the ASP.NET Core host integration for NOF applications, including automatic HTTP endpoint generation from `[HttpEndpoint]` attributes, OpenAPI/Scalar documentation, JSON serialization configuration, middleware pipeline, and the `INOFAppBuilder` implementation for web applications.

## Features

- **Automatic Endpoint Mapping** - source generators turn `[HttpEndpoint]` requests into minimal API endpoints
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

app.MapAllHttpEndpoints();

await app.RunAsync();
```

Requests annotated with `[HttpEndpoint]` are automatically mapped to minimal API endpoints:

```csharp
[HttpEndpoint(HttpVerb.Get, "/api/orders/{id}")]
[RequirePermission("orders:read")]
public record GetOrderRequest(Guid Id);
```

## Installation

```shell
dotnet add package NOF.Hosting.AspNetCore
```

## License

Apache-2.0


