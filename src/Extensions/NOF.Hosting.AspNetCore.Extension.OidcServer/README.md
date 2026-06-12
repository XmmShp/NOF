# NOF.Hosting.AspNetCore.Extension.OidcServer

Authentication authority core and ASP.NET Core OIDC server endpoints for the NOF Framework.

## Overview

This package provides NOF authentication authority capabilities and exposes them as standard HTTP OIDC endpoints instead of projecting them through `IRpcService` contracts.

## Features

- `AddAuthenticationAuthority(...)` registers signing-key persistence, JWT issuing, refresh-token revocation, local JWKS publishing, and key rotation
- `AddOidcServer(...)` registers the OIDC protocol services required by the HTTP surface
- `MapOidcServer()` exposes discovery, authorization, token, userinfo, and JWKS endpoints
- Uses standard ASP.NET Core HTTP behavior for redirects, form posts, status codes, and JSON responses

## Usage

```csharp
using NOF.Hosting.AspNetCore;
using NOF.Hosting.AspNetCore.Extension.OidcServer;

var builder = NOFWebApplicationBuilder.Create(args);

builder.AddAuthenticationAuthority(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
});

builder.AddOidcServer(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.AccessTokenAudience = "your-app";
});

builder.Services.AddScoped<IOAuthAuthorizationHandler, YourAuthorizationHandler>();
builder.Services.AddScoped<IOAuthSubjectService, YourSubjectService>();

var app = await builder.BuildAsync();
app.MapOidcServer();
await app.RunAsync();
```

## License

Apache-2.0
