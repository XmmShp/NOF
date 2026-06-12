# NOF.Infrastructure.Extension.Authentication

JWT authorization and authority extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides JWT infrastructure for NOF applications as a resource server (token validation), as an optional JWT authority (token issuance), and as an OAuth 2.0 / OpenID Connect authorization server implemented through NOF RPC contracts. Outbound token propagation is provided separately by `NOF.Hosting.Extension.Authentication`.

## Features

### Resource Server

- **JWKS Client** - `AddAuthenticationResourceServer()` registers `HttpJwksService` as the default `IJwksService` client
- **Local Key Cache** - `CachedJwksService` keeps signing keys locally so validation does not need to hit the remote JWKS endpoint on every request
- **Token Validation** - `AuthenticationResourceServerInboundMiddleware` validates Bearer tokens with configurable issuer, audience, and lifetime checks
- **Outbound Propagation** - resource server setup also enables access token propagation for downstream NOF calls
- **Local Key Refresh** - when the same process hosts both authority and resource server, local JWKS cache is refreshed immediately after key rotation

### Authority (Server)

- **Token Issuance** - generate access and refresh token pairs with `kid` in JWT header
- **Key Management** - `ISigningKeyService` with RSA key rotation, per-key persistence (`Active` / `Retired` / `Revoked`), encrypted private keys, and stored public keys
- **JWKS Publishing** - expose keys through `IJwksService`
- **Refresh Token Lifecycle** - validate and revoke refresh tokens with cache-based revocation
- **Automatic Key Rotation** - background service rotates keys on a configurable interval

### OAuth 2.0 / OpenID Connect Server

- **Authority Core** - issues JWTs, rotates signing keys, stores authorization codes, and manages refresh-token revocation
- **Discovery Documents** - publishes OAuth authorization server and OIDC metadata
- **JWKS Endpoint** - reuses the JWT authority signing keys
- **Authorization Code Flow** - validates authorization requests, issues cache-backed authorization codes, and redirects with standard OAuth errors
- **PKCE** - supports `plain` and `S256` code challenge verification
- **Token Endpoint** - supports `authorization_code` and rotating `refresh_token` grants
- **OIDC Claims** - emits `id_token` for `openid` requests and `userinfo` from the subject adapter
- **Business Isolation** - user lookup, login UI, consent, tenant rules, external provider binding, and session policy are supplied through interfaces

## Usage

### As a Resource Server

```csharp
builder.AddAuthenticationResourceServer(options =>
{
    options.JwksEndpoint = "https://auth.example.com/.well-known/jwks.json";
    options.Issuer = "your-app";
    options.Audience = "your-audience";
    options.Sources.Add(new AuthenticationTokenSourceOptions
    {
        HeaderName = "Authorization",
        TokenType = "Bearer",
        DownstreamPropagation = new AccessTokenPropagation
        {
            HeaderName = "Authorization",
            TokenType = "Bearer"
        }
    });
    options.Sources.Add(new AuthenticationTokenSourceOptions
    {
        HeaderName = "X-Internal-Jwt",
        TokenType = "Bearer",
        DownstreamPropagation = new AccessTokenPropagation
        {
            HeaderName = "X-Internal-Jwt",
            TokenType = "Bearer"
        }
    });
});
```

The configuration type for this package is `AuthenticationResourceServerOptions`. `JwksEndpoint` is required, and by default the endpoint must use HTTPS.
`Sources` is required for inbound token capture.
If a source does not explicitly configure `DownstreamPropagation`, downstream propagation defaults to the same `HeaderName` and `TokenType` as that source.
If you provide your own `IJwksService`, `AddAuthenticationResourceServer()` preserves it and still layers local caching through `CachedJwksService`.

If you only need outbound propagation and do not need inbound token validation, reference `NOF.Hosting.Extension.Authentication` and use:

```csharp
builder.AddAccessTokenPropagation();
```

You can also keep the values in configuration and read them inside the `AddAuthenticationResourceServer(...)` delegate:

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

The JSON snippet is illustrative only; read these values from `builder.Configuration` in your registration code.

### As a Authentication Authority

```csharp
builder.AddAuthenticationAuthority(options =>
{
    options.Issuer = "your-app";
    options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
});
```

You can also keep the values in configuration and read them inside the `AddAuthenticationAuthority(...)` delegate:

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

The JSON snippet is illustrative only; read these values from `builder.Configuration` in your registration code.

The `SigningKeyEncryptionKey` value can be any non-empty string. NOF deterministically derives an AES key from the configured string, so every instance that shares the signing key database must use the same value.

If `SigningKeyEncryptionKey` is not configured, the authority generates an in-memory 32-byte fallback key for the current process and does not persist this fallback key. For multi-instance or restart-stable deployments, provide `SigningKeyEncryptionKey` via secure configuration (secrets manager, environment variable, or vault).

Signing keys are stored as separate records with status transitions:

- `Active`: current signing key.
- `Retired`: historical validation keys retained based on `RetiredKeyRetentionCount`.
- `Revoked`: keys removed from validation set and deleted later by cleanup.

The persistence step also registers a background cleanup service that periodically deletes old revoked signing keys using `SigningKeyCleanupInterval` and `RevokedSigningKeyRetention`.

### As an OAuth/OIDC Authorization Server

```csharp
builder
    .AddAuthenticationAuthority(options =>
    {
        options.Issuer = "https://auth.example.com/oauth2";
        options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
    })
    .AddOidcServer(options =>
    {
        options.Issuer = "https://auth.example.com/oauth2";
        options.AccessTokenAudience = "your-app";
    });

builder.Services.AddScoped<IOAuthAuthorizationHandler, YourAuthorizationHandler>();
builder.Services.AddScoped<IOAuthSubjectService, YourSubjectService>();

app.MapOidcServer();
```

NOF owns the authority core: authorization-code storage, PKCE validation, refresh-token rotation, JWT access tokens, OIDC `id_token`, and JWKS publishing.

The standard HTTP protocol surface now lives in `NOF.Hosting.AspNetCore.Extension.OidcServer` and is mapped with `app.MapOidcServer()`.

Your application owns the business surface:

- `IOAuthAuthorizationHandler` decides whether the request is already authenticated, must redirect to login/consent, or fails by business policy.
- `IOAuthAuthorizationCodeService` can be injected into your login callback to issue a code after your own login flow succeeds.
- `IOAuthSubjectService` maps an OAuth subject to access-token and identity claims, and can reject refresh-token reuse when your domain session is revoked.

The Koala user service should keep DingTalk/ZJU bridge logic, identity binding, email binding, user creation, role lookup, and session revocation in its own application layer, then adapt those behaviors through the interfaces above. Those concerns are Koala business policy, while NOF keeps only the reusable OAuth/OIDC protocol machinery.

## Dependencies

- [`NOF.Contract.Extension.Authentication`](https://www.nuget.org/packages/NOF.Contract.Extension.Authentication) - JWT contract definitions
- [`NOF.Hosting.Extension.Authentication`](https://www.nuget.org/packages/NOF.Hosting.Extension.Authentication) - outbound access token propagation
- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Infrastructure.Extension.Authentication
```

## License

Apache-2.0
