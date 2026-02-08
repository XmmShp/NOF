# NOF.Extensions.Auth.Jwt

JWT authentication extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides server-side JWT token issuance and lifecycle management. Includes key derivation services using Bouncy Castle, token pair generation (access + refresh tokens), and configurable JWT options.

## Features

- **Token Issuance** — generate access and refresh token pairs
- **Key Derivation** — secure key derivation via Bouncy Castle cryptography
- **Configurable Options** — issuer, audience, expiration, signing algorithms
- **Step Integration** — plugs into the NOF pipeline as a service registration step

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddJwtAuthority();
```

Configure via `JwtOptions` in your application settings:

```json
{
  "Jwt": {
    "Issuer": "your-app",
    "MasterSecurityKey": "your-master-security-key-at-least-32-chars"
  }
}
```

Or configure programmatically:

```csharp
builder.AddJwtAuthority(options =>
{
    options.Issuer = "your-app";
    options.MasterSecurityKey = "your-master-security-key-at-least-32-chars";
});
```

## Dependencies

- [`NOF.Infrastructure.Core`](https://www.nuget.org/packages/NOF.Infrastructure.Core)
- [`NOF.Extensions.Auth.Jwt.Client`](https://www.nuget.org/packages/NOF.Extensions.Auth.Jwt.Client)
- [`Portable.BouncyCastle`](https://www.nuget.org/packages/Portable.BouncyCastle)

## Installation

```shell
dotnet add package NOF.Extensions.Auth.Jwt
```

## License

Apache-2.0
