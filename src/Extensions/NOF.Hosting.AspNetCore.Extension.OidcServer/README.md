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

var app = await builder.BuildAsync();
app.MapOidcServer();
await app.RunAsync();
```

`AddOidcServer(...)` registers a default persisted OAuth client service as `IOAuthClientManagementService`. Applications can create, update, delete, rotate, and validate OAuth clients by resolving that service from DI. Replace `IOAuthClientManagementService` when an application needs custom client validation or management behavior.

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

## License

Apache-2.0
