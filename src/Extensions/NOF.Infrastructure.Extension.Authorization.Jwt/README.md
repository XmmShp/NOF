# NOF.Infrastructure.Extension.Authorization.Jwt

JWT authorization and authority extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides complete JWT infrastructure for NOF applications — both as an OIDC resource server (token validation) and as a JWT authority (token issuance). No ASP.NET Core dependency; works with any NOF host.

## Features

### Resource Server (Client)

- **JWKS Fetching** — `HttpJwksProvider` fetches and caches JSON Web Keys from a remote authority's `/.well-known/jwks.json` endpoint
- **Token Validation** — `JwtIdentityResolver` validates Bearer tokens against cached JWKS keys with configurable issuer, audience, and lifetime checks
- **Identity Propagation** — `JwtClaimsPrincipal` carries the raw token so `JwtAuthorizationOutboundMiddleware` can propagate it to downstream services
- **Key Rotation Support** — `RefreshJwksOnKeyRotation` handler listens for `JwtKeyRotationNotification` and refreshes cached keys automatically

### Authority (Server)

- **Token Issuance** — generate access and refresh token pairs with `kid` in JWT header
- **Key Management** — `ISigningKeyService` with RSA key rotation and retired key retention
- **Local Token Validation** — the authority validates its own tokens using `LocalJwksProvider` without HTTP calls
- **Refresh Token Lifecycle** — validate and revoke refresh tokens with cache-based revocation
- **Automatic Key Rotation** — background service rotates keys on a configurable interval and publishes `JwtKeyRotationNotification` for distributed JWKS refresh

## Usage

### As a Resource Server

```csharp
builder.AddJwtAuthorization();
```

Configure via application settings:

```json
{
  "NOF": {
    "JwtAuthorization": {
      "Authority": "https://auth.example.com",
      "Issuer": "your-app",
      "Audience": "your-audience"
    }
  }
}
```

### As a JWT Authority

```csharp
builder.AddJwtAuthority();
```

Configure via application settings:

```json
{
  "NOF": {
    "Authority": {
      "Issuer": "your-app"
    }
  }
}
```

## Dependencies

- [`NOF.Infrastructure.Core`](https://www.nuget.org/packages/NOF.Infrastructure.Core)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Infrastructure.Extension.Authorization.Jwt
```

## License

Apache-2.0
