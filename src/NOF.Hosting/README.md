# NOF.Hosting

Hosting abstraction package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the core host-builder abstraction contracts and baseline hosting capabilities used by NOF hosting implementations:

- `INOFAppBuilder`
- `IApplicationInitializationStep`
- `DependencyGraph` (dependency-aware topological ordering)
- `JwtClaimsIdentity`, `JwtPropagation`, and `AddJwtPropagation()` for outbound JWT propagation

This package enables host scenarios that do not require the full application/infrastructure stack.

`IServiceCollection.AddNOFHosting()` registers the package-local hosting defaults, while `AddJwtPropagation()` adds the request outbound JWT propagation convenience.

## Installation

```shell
dotnet add package NOF.Hosting
```

## License

Apache-2.0
