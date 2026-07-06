# NOF.Hosting.BlazorWebAssembly

Blazor WebAssembly hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides NOF host integration for Blazor WebAssembly applications by adapting `WebAssemblyHostBuilder` to the NOF builder pipeline.

This package focuses on host bootstrapping. Reusable UI primitives are provided by `NOF.UI`.

## Features

- **NOF Builder Integration** - create and configure NOF apps on top of the built-in Blazor WebAssembly host
- **Underlying Builder Access** - use `builder.WebAssemblyHostBuilder` when you need direct access to root components or browser host settings
- **UI Defaults** - `Create(...)` registers `NOF.UI` services for browser storage and browser-info support

## Usage

```csharp
var builder = NOFWebAssemblyHostBuilder.Create(args);

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.WebAssemblyHostBuilder.HostEnvironment.BaseAddress)
});

var app = await builder.BuildAsync();
await app.RunAsync();
```

`Create(...)` already adds the calling assembly as an application part. Use `AddApplicationPart(...)` only when you need to register additional assemblies.

`Create(...)` also registers `NOF.UI` defaults for browser storage and browser-info support. It does not apply `NOF.Infrastructure` defaults automatically.

## Dependencies

- [`NOF.Hosting`](https://www.nuget.org/packages/NOF.Hosting)
- [`NOF.UI`](https://www.nuget.org/packages/NOF.UI)
- [`Microsoft.AspNetCore.Components.WebAssembly`](https://www.nuget.org/packages/Microsoft.AspNetCore.Components.WebAssembly)

## Installation

```shell
dotnet add package NOF.Hosting.BlazorWebAssembly
```

## License

Apache-2.0
