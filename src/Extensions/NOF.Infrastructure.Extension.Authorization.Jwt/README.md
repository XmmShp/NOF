# NOF.Infrastructure.Extension.Authorization.Jwt

JWT authorization and authority extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides JWT infrastructure for NOF applications as a resource server (token validation) and as an optional JWT authority (token issuance). Outbound token propagation is provided separately by `NOF.Hosting.Extension.Authorization.Jwt`. No ASP.NET Core dependency; works with any NOF host.

## Features

### Resource Server

- **JWKS Client** - `AddJwtResourceServer()` registers `NOF.Contract.Extension.Authorization.Jwt.HttpJwksService` as the default `IJwksService` client
- **JWKS Provider** - `IJwksProvider` caches signing keys locally and serves validation from cache; when the host also acts as an authority it refreshes directly from local signing keys instead of re-calling `IJwksService`
- **Token Validation** - `JwtResourceServerInboundMiddleware` validates Bearer tokens with configurable issuer, audience, and lifetime checks
- **Outbound Propagation** - resource server setup also enables JWT token propagation for downstream NOF calls
- **Key Rotation Refresh** - `RefreshJwksOnKeyRotation` refreshes cached keys when `JwtKeyRotationNotification` is received

### Authority (Server)

- **Token Issuance** - generate access and refresh token pairs with `kid` in JWT header
- **Key Management** - `ISigningKeyService` with RSA key rotation, per-key persistence (`Active` / `Retired` / `Revoked`), encrypted private keys, and stored public keys
- **JWKS Publishing** - expose keys through `IJwksService`
- **Refresh Token Lifecycle** - validate and revoke refresh tokens with cache-based revocation
- **Automatic Key Rotation** - background service rotates keys on a configurable interval

## Usage

### As a Resource Server

```csharp
builder.AddJwtResourceServer(options =>
{
    options.JwksEndpoint = "https://auth.example.com/.well-known/jwks.json";
    options.Issuer = "your-app";
    options.Audience = "your-audience";
});
```

The configuration type for this package is `JwtResourceServerOptions`. `JwksEndpoint` is required, and by default the endpoint must use HTTPS.
If you provide your own `IJwksService`, `AddJwtResourceServer()` will keep it and still layer `IJwksProvider` on top for local caching.

If you only need outbound propagation and do not need inbound token validation, reference `NOF.Hosting.Extension.Authorization.Jwt` and use:

```csharp
builder.AddJwtTokenPropagation();
```

Configure via application settings:

```json
{
  "NOF": {
    "JwtResourceServer": {
      "JwksEndpoint": "https://auth.example.com/.well-known/jwks.json",
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
      "Issuer": "your-app",
      "SigningKeyEncryptionKey": "your-shared-signing-key-passphrase"
    }
  }
}
```

The `SigningKeyEncryptionKey` value can be any non-empty string. NOF deterministically derives an AES key from the configured string, so every instance that shares the signing key database must use the same value.

If `SigningKeyEncryptionKey` is not configured, the authority generates an in-memory 32-byte fallback key for the current process and does not persist this fallback key. For multi-instance or restart-stable deployments, provide `SigningKeyEncryptionKey` via secure configuration (secrets manager, environment variable, or vault).

Signing keys are stored as separate records with status transitions:
- `Active`: current signing key.
- `Retired`: historical validation keys retained based on `RetiredKeyRetentionCount`.
- `Revoked`: keys removed from validation set and deleted later by cleanup.

The persistence step also registers a background cleanup service that periodically deletes old revoked signing keys using `SigningKeyCleanupInterval` and `RevokedSigningKeyRetention`.

## Dependencies

- [`NOF.Contract.Extension.Authorization.Jwt`](https://www.nuget.org/packages/NOF.Contract.Extension.Authorization.Jwt) - JWT contract definitions
- [`NOF.Hosting.Extension.Authorization.Jwt`](https://www.nuget.org/packages/NOF.Hosting.Extension.Authorization.Jwt) - outbound JWT token propagation
- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Infrastructure.Extension.Authorization.Jwt
```

## License

Apache-2.0
