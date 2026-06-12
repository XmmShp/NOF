# NOF.Hosting.Abstraction

Hosting abstraction package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the core host-builder abstraction contracts and baseline hosting capabilities used by NOF hosting implementations:

- `INOFAppBuilder`
- `IServiceRegistrationContext`
- `IAfter<>` / `IBefore<>`
- `IServiceRegistrationStep`
- `IApplicationInitializationStep`
- `DependencyGraph` (dependency-aware topological ordering)
- `JwtClaimsIdentity`, `JwtPropagation`, and `AddJwtPropagation()` for outbound JWT propagation

This package enables host scenarios that do not require the full application/infrastructure stack.

## Installation

```shell
dotnet add package NOF.Hosting.Abstraction
```

## License

Apache-2.0
