# NOF.Hosting

Hosting abstraction package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the core host-builder abstraction contracts and baseline hosting capabilities used by NOF hosting implementations:

- `IHostApplicationBuilder`
- `IApplicationInitializationStep`
- `DependencyGraph` (dependency-aware topological ordering)
- `JwtClaimsIdentity` as a raw JWT-carrying identity type

This package enables host scenarios that do not require the full application/infrastructure stack.

`IServiceCollection.AddNOFHosting()` registers the package-local hosting defaults. Token forwarding, token exchange, and service-token acquisition are intentionally not implemented in this package.

## Installation

```shell
dotnet add package NOF.Hosting
```

## License

Apache-2.0
