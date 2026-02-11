# NOF.Hosting.AspNetCore.Extensions.Authority

ASP.NET Core JWT authority hosting extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides a complete JWT authority for ASP.NET Core hosted NOF applications. Includes token issuance, RSA key management with rotation, standard `/.well-known/jwks.json` endpoint, refresh token validation/revocation, and local token validation without HTTP round-trips. JWT client infrastructure (OIDC resource server, JWKS fetching) is built into `NOF.Infrastructure.Core`.

## Features

- **Token Issuance** — generate access and refresh token pairs with `kid` in JWT header
- **Key Management** — `ISigningKeyService` with RSA key rotation and retired key retention
- **Standard JWKS Endpoint** — `/.well-known/jwks.json` served directly via minimal API, compatible with OIDC clients
- **Local Token Validation** — the authority validates its own tokens using `LocalJwksProvider` without HTTP calls
- **Refresh Token Lifecycle** — validate and revoke refresh tokens with cache-based revocation
- **Key Rotation** — via NOF notification pub/sub (`KeyRotationNotification`)
- **Service Registration** — `AddJwtAuthority()` registers all required services and the JWKS endpoint in one call

## Usage

```csharp
var builder = NOFWebApplicationBuilder.Create(args, useDefaultConfigs: true);

builder.AddJwtAuthority();
```

Configure via `JwtOptions` in your application settings:

```json
{
  "Jwt": {
    "Issuer": "your-app"
  }
}
```

Or configure programmatically:

```csharp
builder.AddJwtAuthority(options =>
{
    options.Issuer = "your-app";
    options.KeySize = 2048;
    options.RetiredKeyRetentionCount = 2;
});
```

## Dependencies

- [`NOF.Hosting.AspNetCore`](https://www.nuget.org/packages/NOF.Hosting.AspNetCore)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Hosting.AspNetCore.Extensions.Authority
```

## License

Apache-2.0
