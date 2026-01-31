# NOF.Extensions.Auth.Jwt.Client

A lightweight JWT authentication client extension for the NOF framework that provides client-side JWT token management services using NOF's RequestSender pattern.

## Features

- **Client-Side Operations**: All JWT operations available through RequestSender
- **Lightweight**: Minimal dependencies, only requires NOF.Application
- **Request-Based API**: Uses NOF's Request/Response pattern for all operations
- **Service Abstraction**: Provides JwtClientService for easy integration

## Installation

```bash
dotnet add package NOF.Extensions.Auth.Jwt.Client
```

## Quick Start

### 1. Configure Services

```csharp
// In your service configuration
services.AddJwtClient();

// Make sure you have RequestSender configured (usually part of NOF infrastructure)
```

### 2. Use JwtClientService

```csharp
public class AuthService
{
    private readonly JwtClientService _jwtClient;

    public AuthService(JwtClientService jwtClient)
    {
        _jwtClient = jwtClient;
    }

    public async Task<TokenPair?> LoginAsync(LoginRequest request)
    {
        // Validate user credentials (your own logic)
        
        return await _jwtClient.GenerateTokenAsync(request.UserId, 
            roles: new List<string> { "user", "admin" },
            scopes: new List<string> { "read", "write" },
            customClaims: new Dictionary<string, string>
            {
                ["email"] = request.Email,
                ["name"] = request.Name
            });
    }

    public async Task<TokenPair?> RefreshTokenAsync(string refreshToken)
    {
        return await _jwtClient.RefreshTokenAsync(refreshToken);
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        return await _jwtClient.ValidateTokenAsync(token);
    }

    public async Task<bool> RevokeTokenAsync(string tokenId)
    {
        return await _jwtClient.RevokeTokenAsync(tokenId);
    }

    public async Task<bool> RevokeUserTokensAsync(string userId)
    {
        return await _jwtClient.RevokeUserTokensAsync(userId);
    }

    public async Task<string?> GetJwksAsync()
    {
        return await _jwtClient.GetJwksAsync();
    }
}
```

### 3. Use RequestSender Directly

You can also use RequestSender directly with the request types from NOF.Extensions.Auth.Jwt:

```csharp
public class MyService
{
    private readonly IRequestSender _requestSender;

    public MyService(IRequestSender requestSender)
    {
        _requestSender = requestSender;
    }

    public async Task<TokenPair?> GenerateTokenAsync(string userId)
    {
        var request = new GenerateJwtTokenRequest
        {
            UserId = userId,
            Roles = new List<string> { "user" },
            Scopes = new List<string> { "read" }
        };

        var result = await _requestSender.SendAsync<GenerateJwtTokenResponse>(request);
        
        return result.IsSuccess ? result.Value?.TokenPair : null;
    }
}
```

## Available Methods

### JwtClientService Methods

- `GenerateTokenAsync(userId, roles?, scopes?, customClaims?)` - Generate JWT token pair
- `RefreshTokenAsync(refreshToken)` - Refresh access token
- `ValidateTokenAsync(token)` - Validate JWT token
- `RevokeTokenAsync(tokenId)` - Revoke specific token
- `RevokeUserTokensAsync(userId)` - Revoke all user tokens
- `GetJwksAsync()` - Get JWKS for validation

### Request Types (from NOF.Extensions.Auth.Jwt)

- `GenerateJwtTokenRequest` / `GenerateJwtTokenResponse`
- `RefreshJwtTokenRequest` / `RefreshJwtTokenResponse`
- `ValidateJwtTokenRequest` / `ValidateJwtTokenResponse`
- `RevokeJwtTokenRequest` / `RevokeJwtTokenResponse`
- `RevokeUserJwtTokensRequest` / `RevokeUserJwtTokensResponse`
- `GetJwksRequest` / `GetJwksResponse`

## Architecture

This client package is designed to work with the server-side `NOF.Extensions.Auth.Jwt` package through NOF's messaging infrastructure:

```
Client Application
├── NOF.Extensions.Auth.Jwt.Client
│   ├── JwtClientService
│   └── Request/Response Types
├── NOF RequestSender
└── Network/Transport
    ↓
Server Application
├── NOF.Extensions.Auth.Jwt
│   ├── JwtTokenService
│   ├── JwksService
│   └── Request Handlers
└── NOF Request Handlers
```

## Dependencies

- Microsoft.Extensions.DependencyInjection.Abstractions
- NOF.Application
- NOF.Extensions.Auth.Jwt (for request/response types)

## License

This package is part of the NOF Framework and is licensed under the MIT License.
