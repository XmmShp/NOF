---
description: Add JWT authority/resource-server capabilities to a NOF application
---

# Add JWT Auth

NOF supports two JWT modes:

- authority mode (issue/revoke/introspect tokens)
- resource-server mode (validate tokens and propagate them downstream)

## 1. Add Packages

```bash
dotnet add package NOF.Infrastructure.Extension.Authorization.Jwt
dotnet add package NOF.Contract.Extension.Authorization.Jwt
```

## 2. Register in Program.cs

```csharp
builder.AddJwtAuthority(o =>
{
    o.Issuer = "MyApp";
    o.SigningKeyEncryptionKey = builder.Configuration["NOF:Authority:SigningKeyEncryptionKey"]
        ?? throw new InvalidOperationException("Configuration value 'NOF:Authority:SigningKeyEncryptionKey' not found.");
});

builder.AddJwtResourceServer(o =>
{
    o.Issuer = "MyApp";
    o.RequireHttpsMetadata = false;
    o.JwksEndpoint = "http://localhost/.well-known/jwks.json";
});
```

## 3. Expose HTTP Endpoints Explicitly

```csharp
app.MapHttpEndpoint<JwtAuthorityService>();
app.MapGet("/.well-known/jwks.json", async (IJwksService jwksService, CancellationToken cancellationToken) =>
{
    var document = await jwksService.GetJwksAsync(cancellationToken);
    return Results.Ok(document);
});
```

## 4. Access Identity in Handlers

Inject:

- `IUserContext` for the current principal and permissions
- `IExecutionContext` for tenant and tracing headers

## Notes

- `AddJwtAuthority(...)` automatically adds the authority assembly as an application part.
- `AddJwtResourceServer(...)` also enables outbound token propagation.
- Key rotation notifications use `JwtKeyRotationNotification`.
