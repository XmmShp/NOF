# NOF.Extensions.Auth.Jwt.Client

JWT client extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides client-side JWT utilities for NOF applications. Includes a `GetJwksAsync` extension on `INOFAppBuilder` to retrieve JSON Web Key Sets (JWKS) during application startup, enabling client applications to validate tokens issued by the server-side `NOF.Extensions.Auth.Jwt` package.

## Features

- **JWKS Retrieval** — fetch JSON Web Key Sets for a given audience at startup
- **Token Infrastructure** — shared models and contracts for JWT token handling

## Dependencies

- [`NOF.Infrastructure.Core`](https://www.nuget.org/packages/NOF.Infrastructure.Core)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Extensions.Auth.Jwt.Client
```

## License

Apache-2.0
