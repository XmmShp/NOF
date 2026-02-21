---
description: How to add JWT authentication and authorization to a NOF application
---

# Add JWT Authorization

NOF provides both JWT **authority** (token issuance) and JWT **authorization** (token validation) capabilities.

## Option A: JWT Authority (Issue + Validate Tokens)

Use this when your service is the identity provider.

### 1. Add Package

```bash
dotnet add package NOF.Infrastructure.Extension.Authorization.Jwt
```

### 2. Register in Program.cs

```csharp
using NOF.Infrastructure.Extension.Authorization.Jwt;

builder.AddJwtAuthority()
    .AddJwksRequestHandler();  // Exposes /.well-known/jwks.json endpoint

builder.AddJwtAuthorization();  // Also validate tokens locally
```

### 3. Configure in appsettings.json

```json
{
  "NOF": {
    "Jwt": {
      "Authority": {
        "Issuer": "https://myapp.example.com",
        "AccessTokenLifetime": "01:00:00",
        "RefreshTokenLifetime": "30.00:00:00",
        "KeyRotationInterval": "7.00:00:00"
      }
    }
  }
}
```

### 4. Issue Tokens

Use the built-in request handlers:

```csharp
// Generate a token pair
var result = await _requestSender.SendAsync(new GenerateJwtTokenRequest
{
    Subject = userId,
    Claims = new Dictionary<string, string>
    {
        ["role"] = "admin",
        ["tenant"] = tenantId
    }
});
// result.Value.AccessToken, result.Value.RefreshToken

// Validate and refresh
var refreshResult = await _requestSender.SendAsync(
    new ValidateJwtRefreshTokenRequest(refreshToken));

// Revoke a refresh token
await _requestSender.SendAsync(
    new RevokeJwtRefreshTokenRequest(refreshToken));
```

## Option B: JWT Authorization Only (Validate Tokens from External Authority)

Use this when tokens are issued by another service.

### 1. Register in Program.cs

```csharp
builder.AddJwtAuthorization();
```

### 2. Configure in appsettings.json

```json
{
  "NOF": {
    "Jwt": {
      "Authorization": {
        "Issuer": "https://auth.example.com",
        "JwksEndpoint": "https://auth.example.com/.well-known/jwks.json"
      }
    }
  }
}
```

Or configure inline:
```csharp
builder.AddJwtAuthorization("https://auth.example.com/.well-known/jwks.json");
```

## 3. Protecting Endpoints

By default, all endpoints require authentication. Use `[AllowAnonymous]` to opt out:

```csharp
// Protected (default)
[ExposeToHttpEndpoint(HttpVerb.Get, "api/orders")]
public record GetOrdersRequest : IRequest<GetOrdersResponse>;

// Public
[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post, "api/auth/login")]
public record LoginRequest(string Username, string Password) : IRequest<LoginResponse>;
```

## 4. Accessing Identity in Handlers

The current user identity is available via `IInvocationContext`:

```csharp
using NOF.Application;
using System.Security.Claims;

public class GetMyOrdersHandler : IRequestHandler<GetMyOrdersRequest, GetMyOrdersResponse>
{
    private readonly IInvocationContext _context;

    public GetMyOrdersHandler(IInvocationContext context)
    {
        _context = context;
    }

    public async Task<Result<GetMyOrdersResponse>> HandleAsync(
        GetMyOrdersRequest request, CancellationToken ct)
    {
        var user = _context.User;           // ClaimsPrincipal
        var tenantId = _context.TenantId;   // string?
        var traceId = _context.TraceId;     // string?
        // Query orders for this user...
    }
}
```

## Notes

- JWT keys are automatically rotated via a background service when using Authority mode.
- JWKS endpoint is auto-exposed at `/.well-known/jwks.json` when `AddJwksRequestHandler()` is called.
- Key rotation publishes a `JwtKeyRotationNotification` so other services can refresh their cached keys.
- The authorization middleware runs in the NOF inbound pipeline — it works for both HTTP and MassTransit messages.
- Refresh token revocation uses `ICacheService` by default — override `IRevokedRefreshTokenRepository` for custom storage.
