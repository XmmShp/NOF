# NOF.Hosting.AspNetCore.Extension.OidcServer

Authentication authority core and ASP.NET Core OIDC server endpoints for the NOF Framework.

## Overview

This package provides NOF authentication authority capabilities and exposes them as standard HTTP OIDC endpoints instead of projecting them through `IRpcService` contracts.

## Features

- `AddOidcServer(...)` registers signing-key persistence, JWT issuing, refresh-token revocation, local JWKS publishing, key rotation, OIDC protocol services, and default persisted OAuth client management services
- `MapOidcServer()` exposes discovery, authorization, token, revocation, introspection, userinfo, and JWKS endpoints
- Supports `authorization_code`, `refresh_token`, `client_credentials`, and `token_exchange` grants
- Uses standard ASP.NET Core HTTP behavior for redirects, form posts, status codes, and JSON responses

## Usage

```csharp
using NOF.Hosting.AspNetCore;
using NOF.Hosting.AspNetCore.Extension.OidcServer;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddOidcServer(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.AccessTokenAudience = "your-app";
    options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
});

builder.Services.AddScoped<IOAuthAuthorizationHandler, YourAuthorizationHandler>();
builder.Services.AddScoped<IOAuthSubjectService, YourSubjectService>();
builder.Services.AddScoped<IOAuthTokenExchangeHandler, YourTokenExchangeHandler>();

var app = await builder.BuildAsync();
app.MapOidcServer();
await app.RunAsync();
```

`AddOidcServer(...)` registers a default persisted OAuth client service as `IOAuthClientManagementService` and a default `IOAuthTokenExchangeHandler`. Applications can replace either service when they need custom client validation, management behavior, or token-exchange claim construction.

Bootstrap helpers are available on the returned selector:

```csharp
builder.AddOidcServer(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.AccessTokenAudience = "your-app";
    options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
})
.AddPublicClient("spa-client", ["openid", "profile", "api.read"])
.AddConfidentialClient("service-client", "service-client-secret", ["api.read", "api.write"]);
```

`ITokenService` accepts explicit multi-value claims through `TokenClaim.Array(...)`. The issuer expands those values into repeated same-name claims so the resulting JWT payload is emitted as a standard JSON array claim.

`ITokenService` also accepts explicit JSON object claims through `TokenClaim.Json(...)`. The default token-exchange handler emits the standard chained `act` claim for confidential clients, omits `act` for public clients, and by default issues client-credentials subjects in the form `client:{client_id}`.

## License

Apache-2.0
