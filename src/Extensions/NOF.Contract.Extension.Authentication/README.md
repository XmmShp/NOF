# NOF.Contract.Extension.Authentication

JWT contract extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides request/response models and service interfaces for JWT authority operations, including:

- Authority token issuance contracts
- Refresh-token validation and revocation contracts
- Shared token payload models

This package contains contracts only. It does not provide JWKS publishing, token validation middleware, or hosting integration.

## Usage

Reference this package in projects that define or consume JWT authority contracts.

For implementation and runtime behavior:

- Use `NOF.Infrastructure.Extension.Authentication` for JWT validation and authority services
- Use `NOF.Hosting.Extension.Authentication` for outbound token propagation

## Dependencies

- [`NOF.Contract`](https://www.nuget.org/packages/NOF.Contract)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Contract.Extension.Authentication
```

## License

Apache-2.0
