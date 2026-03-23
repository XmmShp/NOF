# NOF.UI

Reusable UI package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides shared UI-facing capabilities for browser clients:

- Authorization components (`AuthGuard`, `PermissionRouteView`)
- Browser storage abstractions (`ILocalStorage`, `ISessionStorage`)
- HTTP request authorization handler for client `HttpClient` pipelines

`NOF.Hosting.BlazorWebAssembly` depends on this package and focuses on host bootstrapping.

## Installation

```shell
dotnet add package NOF.UI
```

## License

Apache-2.0

