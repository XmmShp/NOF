# NOF.Hosting.Abstraction

Hosting abstraction package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the core host-builder abstraction contracts used by NOF hosting implementations:

- `INOFAppBuilder`
- `IServiceRegistrationContext`
- `IStep` / `IAfter<>` / `IBefore<>`
- `IServiceRegistrationStep`
- `IApplicationInitializationStep`
- `ConfiguratorGraph<T>` (dependency-aware topological ordering for steps)
- `NOFServiceProviderFactory` / `NOFServiceProvider` (provider wrapper for initialization and daemon resolution)

This package enables host scenarios that do not require the full application/infrastructure stack.

## Provider Behavior

`NOFAppBuilder` configures container creation with `NOFServiceProviderFactory` by default.

- Services implementing `IInitializable` are initialized when first resolved.
- Services registered as `IDaemonService` are eagerly resolved whenever a NOF service provider is created.

## Installation

```shell
dotnet add package NOF.Hosting.Abstraction
```

## License

Apache-2.0

