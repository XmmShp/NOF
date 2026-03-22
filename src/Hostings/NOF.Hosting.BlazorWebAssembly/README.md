# NOF.Hosting.BlazorWebAssembly

Blazor WebAssembly hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides NOF host integration for Blazor WebAssembly applications by adapting `WebAssemblyHostBuilder` to the NOF builder pipeline.

This package focuses on host bootstrapping. Reusable UI primitives are provided by `NOF.UI`.

## Features

- **NOF Builder Integration** - create and configure NOF apps on top of Blazor WebAssembly hosting
- **AOT-friendly setup** - aligns with WebAssembly and trimming scenarios
- **Source generator support** - includes NOF hosting source generator packing

## Usage

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

var nofBuilder = NOFWebAssemblyHostBuilder.Create(builder);

nofBuilder.WithAutoApplicationParts();

await nofBuilder.BuildWebAssemblyHostAsync();
```

## Dependencies

- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`NOF.UI`](https://www.nuget.org/packages/NOF.UI)
- [`Microsoft.AspNetCore.Components.WebAssembly`](https://www.nuget.org/packages/Microsoft.AspNetCore.Components.WebAssembly)

## Installation

```shell
dotnet add package NOF.Hosting.BlazorWebAssembly
```

## License

Apache-2.0

