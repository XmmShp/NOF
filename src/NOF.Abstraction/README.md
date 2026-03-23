# NOF.Abstraction

Cross-cutting abstractions package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides shared contracts and annotations intended for use across layers:

- `IInitializable`
- `IDaemonService`
- `[AutoInject]`
- `[AssemblyPrefix]`

## Runtime Behavior

When host applications are built through `NOFAppBuilder`, NOF wraps the service provider with `NOFServiceProvider`.

- Services implementing `IInitializable` are initialized on first resolution.
- Services registered as `IDaemonService` are eagerly resolved whenever a NOF service provider is created.

## Installation

```shell
dotnet add package NOF.Abstraction
```

## License

Apache-2.0
