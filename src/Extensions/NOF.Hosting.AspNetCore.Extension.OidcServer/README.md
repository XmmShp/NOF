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

`options.Issuer` is the final issuer identifier published in discovery metadata and embedded into issued tokens. It should usually include the OIDC path segment such as `/oauth2`. `options.PathBase` only controls where the local endpoints are mapped and is not appended to `Issuer` automatically.

`AddOidcServer(...)` registers a default persisted OAuth client service as `IOAuthClientManagementService` and a default `IOAuthTokenExchangeHandler`. Applications can replace either service when they need custom client validation, management behavior, or token-exchange claim construction.

Bootstrap helpers are available on the returned selector:

```csharp
builder.AddOidcServer(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.AccessTokenAudience = "your-app";
    options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
})
.AddPublicClient(
    "spa-client",
    ["openid", "profile", "api.read"],
    redirectUris: ["https://app.example.com/oauth/callback"])
.AddConfidentialClient(
    "service-client",
    "service-client-secret",
    ["api.read", "api.write"],
    redirectUris: ["https://service.example.com/oauth/callback"]);
```

Authorization requests may omit `redirect_uri` when the client has exactly one registered redirect URI; in that case the server uses the registered value automatically. When `redirect_uri` is supplied, it must exactly match one of the client's registered `RedirectUris`. The default persisted client management service rejects non-absolute redirect URIs at create/update time, and the authorization endpoint refuses to redirect to unregistered callback URLs.

`ITokenService` accepts explicit multi-value claims through `TokenClaim.Array(...)`. The issuer expands those values into repeated same-name claims so the resulting JWT payload is emitted as a standard JSON array claim.

`ITokenService` also accepts explicit JSON object claims through `TokenClaim.Json(...)`. The default token-exchange handler emits the standard chained `act` claim for confidential clients, omits `act` for public clients, and by default issues client-credentials subjects in the form `client:{client_id}`.

## License

Apache-2.0
