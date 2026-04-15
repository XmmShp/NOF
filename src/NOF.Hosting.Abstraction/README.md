# NOF.Hosting.Abstraction

Hosting abstraction package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the core host-builder abstraction contracts used by NOF hosting implementations:

- `INOFAppBuilder`
- `IServiceRegistrationContext`
- `IAfter<>` / `IBefore<>`
- `IServiceRegistrationStep`
- `IApplicationInitializationStep`
- `DependencyGraph` (dependency-aware topological ordering)

This package enables host scenarios that do not require the full application/infrastructure stack.

## Installation

```shell
dotnet add package NOF.Hosting.Abstraction
```

## License

Apache-2.0
