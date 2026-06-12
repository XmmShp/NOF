# NOF.Infrastructure.Extension.Authentication

JWT resource server extension for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides JWT resource-server infrastructure for NOF applications: JWKS fetching, local validation-key caching, inbound token validation, identity resolution, and downstream access-token propagation.

Authority/token-issuing capabilities live in `NOF.Hosting.AspNetCore.Extension.OidcServer`. Outbound-only token propagation lives in `NOF.Hosting.Extension.Authentication`.

## Features

- **JWKS Client** - `AddAuthenticationResourceServer()` registers `HttpJwksService` as the default `IJwksService` client
- **Local Key Cache** - `ResourceServerJwksCacheService` keeps signing keys locally so validation does not need to hit the remote JWKS endpoint on every request
- **Token Validation** - `AuthenticationResourceServerInboundMiddleware` validates Bearer tokens with configurable issuer, audience, and lifetime checks
- **Outbound Propagation** - resource server setup also enables access token propagation for downstream NOF calls

## Usage

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
});
```

The configuration type for this package is `AuthenticationResourceServerOptions`. `JwksEndpoint` is required for remote authorities. `Sources` controls which inbound headers are accepted.

If a source does not explicitly configure `DownstreamPropagation`, downstream propagation defaults to the same `HeaderName` and `TokenType` as that source. If you provide your own `IJwksService`, `AddAuthenticationResourceServer()` preserves it and layers local caching through `ResourceServerJwksCacheService`.

## Dependencies

- [`NOF.Hosting.AspNetCore.Extension.OidcServer`](https://www.nuget.org/packages/NOF.Hosting.AspNetCore.Extension.OidcServer) - OIDC service contracts and JWKS models
- [`NOF.Hosting.Extension.Authentication`](https://www.nuget.org/packages/NOF.Hosting.Extension.Authentication) - outbound access token propagation
- [`NOF.Infrastructure`](https://www.nuget.org/packages/NOF.Infrastructure)
- [`System.IdentityModel.Tokens.Jwt`](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt)

## Installation

```shell
dotnet add package NOF.Infrastructure.Extension.Authentication
```

## License

Apache-2.0
