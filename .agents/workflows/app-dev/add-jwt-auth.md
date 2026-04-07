---
description: Add JWT authority/resource-server capabilities to a NOF application
---

# Add JWT Auth

NOF has two JWT modes:
- authority mode (issue/revoke/introspect tokens)
- resource-server mode (validate external or local-issued tokens)

## 1. Add Package

```bash
dotnet add package NOF.Infrastructure.Extension.Authorization.Jwt
dotnet add package NOF.Contract.Extension.Authorization.Jwt
```

## 2. Register in Program.cs

```csharp
builder.AddJwtAuthority(o => o.Issuer = "MyApp");

builder.AddJwtResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
});
```

## 3. Map Services to HTTP Endpoints

```csharp
app.MapServiceToHttpEndpoints<IJwtAuthorityService>();
app.MapServiceToHttpEndpoints<IJwksService>();
```

## 4. Issue and Validate Tokens

```csharp
var issue = await _requestSender.SendAsync(new GenerateJwtTokenRequest(
    UserId: "u-1",
    TenantId: "t-1",
    Audience: "my-api",
    AccessTokenExpiration: TimeSpan.FromMinutes(30),
    RefreshTokenExpiration: TimeSpan.FromDays(7)));

var validate = await _requestSender.SendAsync(
    new ValidateJwtRefreshTokenRequest(issue.Value!.TokenPair.RefreshToken));
```

## 5. Access Identity in Handlers

Inject:
- `IUserContext` for current principal and permissions
- `IExecutionContext` for tenant and tracing headers

## 6. Notes

- `AddJwksRequestHandler()` currently returns the selector for chaining compatibility.
- JWKS fetch + cache is handled by `IJwksProvider`.
- Key rotation notification is `JwtKeyRotationNotification`.
