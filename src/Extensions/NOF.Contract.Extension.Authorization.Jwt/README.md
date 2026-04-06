# NOF.Contract.Extension.Authorization.Jwt

JWT contract extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides request/response models and service interfaces for JWT operations, including:

- Authority token issuance contracts
- JWKS retrieval contracts
- Key-rotation notifications

This package contains contracts only and no JWT validation or hosting middleware implementation.

## Usage

Reference this package in projects that define or consume JWT authority/JWKS contracts.

For implementation and runtime behavior:

- Use `NOF.Infrastructure.Extension.Authorization.Jwt` for JWT validation and authority services
- Use `NOF.Hosting.Extension.Authorization.Jwt` for outbound token propagation

## Dependencies

- [`NOF.Contract`](https://www.nuget.org/packages/NOF.Contract)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Contract.Extension.Authorization.Jwt
```

## License

Apache-2.0
