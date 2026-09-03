---
description: Add the built-in OAuth/OIDC server and JWT resource-server validation to a NOF ASP.NET Core application
---

# Add OAuth/OIDC Authentication

The current authentication surface has two independent pieces:

- `NOF.Hosting.AspNetCore.Extension.OidcServer`: local OAuth 2.0 / OpenID Connect authority
- `NOF.Infrastructure`: JWT resource-server validation for RPC, command, and notification pipelines

## 1. Add the OIDC Server Package

```bash
dotnet add package NOF.Hosting.AspNetCore.Extension.OidcServer
```

Configure a durable `IDbContext` provider before production use; signing keys, clients, and revoked refresh tokens are persisted.

## 2. Register the Authority and a Client

```csharp
using NOF.Hosting;
using NOF.Hosting.AspNetCore.Extension.OidcServer;

var issuer = "https://auth.example.com/oauth2";

builder.AddOidcServer(options =>
{
    options.Issuer = issuer;
    options.PathBase = "/oauth2";
    options.AccessTokenAudience = "my-app";
    options.SigningKeyEncryptionKey = builder.Configuration["NOF:OidcServer:SigningKeyEncryptionKey"]
        ?? throw new InvalidOperationException("OIDC signing-key encryption key not found.");
})
.AddPublicClient(
    "my-app-ui",
    ["openid", "profile", "email", "my-app.read"],
    displayName: "My App UI",
    redirectUris: ["https://app.example.com/oauth/callback"]);
```

The selector also supports confidential, device-flow, and private-key JWT clients. The OIDC initialization step maps metadata, JWKS, authorize, device, token, revoke, introspect, userinfo, and dynamic-registration endpoints automatically.

Do not call the old `AddAuthenticationAuthority(...)` or manually map `TokenAuthorityService`; those APIs are not the current authority surface.

## 3. Provide User Interaction and Subject Data

The default authorize endpoint deliberately fails until the application replaces `IOAuthAuthorizeEndpoint`. For authorization-code/OIDC flows, also implement `IOAuthSubjectService` to supply subject claims. Device flow requires an `IOAuthDeviceVerificationEndpoint` implementation for user interaction.

```csharp
builder.Services.AddScoped<IOAuthAuthorizeEndpoint, MyAuthorizeEndpoint>();
builder.Services.AddScoped<IOAuthSubjectService, MySubjectService>();
```

Use `OAuthAuthorizationCodeIssuer` from the authorize endpoint after the application has authenticated the user and obtained consent.

## 4. Register JWT Validation

```csharp
builder.Services.AddAuthenticationResourceServer(options =>
{
    options.AuthorizationServerIssuer = issuer;
    options.ExpectedIssuer = issuer;
    options.Audience = "my-app";
    options.RequireHttpsMetadata = true;
});
```

The resource server discovers metadata and JWKS from `AuthorizationServerIssuer`. When it is colocated with `AddOidcServer(...)`, the local metadata/JWKS services remain registered.

## 5. Access Identity and Tenant State

```csharp
public sealed class GetProfile(IUserContext userContext, ICurrentTenant currentTenant)
{
    public string? UserId => userContext.User.Id;
    public IReadOnlyList<string> Permissions => userContext.User.Permissions;
    public string TenantId => currentTenant.TenantId;
}
```

`ITransparentInfos` and the old `JwksEndpoint` option are not part of the current public API. The resource-server middleware populates `IUserContext`; authorization/tenant middleware derives trusted tenant state for `ICurrentTenant`.
