# NOF.Hosting.Extension.Authorization.Jwt

JWT hosting extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides outbound JWT token propagation for NOF applications. When the current user is a `JwtClaimsPrincipal`, the outbound middleware automatically writes the `Authorization` header for downstream NOF calls.

This package does not perform inbound token validation and does not provide authority services. If you need inbound validation, use `NOF.Infrastructure.Extension.Authorization.Jwt`.

## Usage

```csharp
builder.AddJwtTokenPropagation();
```

Optional configuration:

```csharp
builder.AddJwtTokenPropagation(options =>
{
    options.HeaderName = "Authorization";
    options.TokenType = "Bearer";
});
```

The configuration type for this package is `JwtTokenPropagationOptions`.

## When To Use

- Use `NOF.Contract.Extension.Authorization.Jwt` for pure JWT service contracts
- Use `NOF.Hosting.Extension.Authorization.Jwt` for outbound token propagation only
- Use `NOF.Infrastructure.Extension.Authorization.Jwt` for resource server inbound validation and optional authority services

## Installation

```shell
dotnet add package NOF.Hosting.Extension.Authorization.Jwt
```

## License

Apache-2.0
