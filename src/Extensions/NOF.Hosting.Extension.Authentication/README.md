# NOF.Hosting.Extension.Authentication

JWT hosting extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides outbound access token propagation for NOF applications. When the current user contains a `AccessTokenIdentity`, the outbound middleware automatically writes the `Authorization` header for downstream NOF calls.

This package does not perform inbound token validation and does not provide authority services. If you need inbound validation, use `NOF.Infrastructure.Extension.Authentication`.

## Usage

```csharp
builder.AddAccessTokenPropagation();
```

Downstream propagation settings are taken from each `AccessTokenIdentity` instance. If `DownstreamPropagation` is null, that identity is not propagated.

## When To Use

- Use `NOF.Contract.Extension.Authentication` for pure JWT service contracts
- Use `NOF.Hosting.Extension.Authentication` for outbound token propagation only
- Use `NOF.Infrastructure.Extension.Authentication` for resource server inbound validation and optional authority services

## Installation

```shell
dotnet add package NOF.Hosting.Extension.Authentication
```

## License

Apache-2.0
