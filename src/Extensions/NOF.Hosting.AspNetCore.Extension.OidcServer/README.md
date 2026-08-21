# NOF.Hosting.AspNetCore.Extension.OidcServer

Authentication authority core and ASP.NET Core OIDC server endpoints for the NOF Framework.

## Overview

This package provides NOF authentication authority capabilities and exposes them as standard HTTP OIDC endpoints instead of projecting them through `IRpcService` contracts.

## Features

- `AddOidcServer(...)` registers signing-key persistence, JWT issuing, refresh-token revocation, local JWKS publishing, key rotation, OIDC protocol services, and default persisted OAuth client management services
- `MapOidcServer()` exposes discovery, authorization, token, revocation, introspection, userinfo, JWKS, and dynamic client registration endpoints
- Supports `authorization_code`, `refresh_token`, `client_credentials`, `device_code`, and `token_exchange` grants
- Supports RFC 7591 client registration and RFC 7592 client configuration management
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

builder.Services.AddScoped<IOAuthAuthorizeEndpoint, YourAuthorizeEndpoint>();
builder.Services.AddScoped<IOAuthSubjectService, YourSubjectService>();
builder.Services.AddScoped<IOAuthTokenExchangeHandler, YourTokenExchangeHandler>();

var app = await builder.BuildAsync();
app.MapOidcServer();
await app.RunAsync();
```

`options.Issuer` is the final issuer identifier published in discovery metadata and embedded into issued tokens. It should usually include the OIDC path segment such as `/oauth2`. `options.PathBase` only controls where the local endpoints are mapped and is not appended to `Issuer` automatically.

When the same application also calls `AddAuthenticationResourceServer(...)`, token validation reads the co-located server metadata and signing keys directly from dependency injection. It does not make a backchannel HTTP request to the public issuer. `ExpectedIssuer` and `Audience` still control issuer and audience validation and should match `Issuer` and `AccessTokenAudience`, respectively.

`AddOidcServer(...)` registers a default persisted OAuth client repository as `IOAuthClientRepository` and a default `IOAuthTokenExchangeHandler`. Applications can replace either service when they need custom client validation, management behavior, or token-exchange claim construction.

## Dynamic client registration

Dynamic client registration is enabled whenever `MapOidcServer()` is used. The discovery document publishes `registration_endpoint`, and the following endpoints are mapped under `PathBase`:

- `POST /oauth2/register` creates a client according to RFC 7591.
- `GET /oauth2/register/{clientId}` reads its configuration according to RFC 7592.
- `PUT /oauth2/register/{clientId}` performs a full metadata replacement.
- `DELETE /oauth2/register/{clientId}` removes the client.

The default `POST` policy requires a NOF access token whose audience is `AccessTokenAudience` and whose scopes include `client.register`. A confidential bootstrap client can be granted that scope and use its access token as the Initial Access Token:

```http
POST /oauth2/register HTTP/1.1
Authorization: Bearer <initial-access-token>
Content-Type: application/json

{
  "redirect_uris": ["https://app.example.com/oauth/callback"],
  "token_endpoint_auth_method": "none",
  "grant_types": ["authorization_code", "refresh_token"],
  "response_types": ["code"],
  "client_name": "Example App",
  "scope": "openid profile offline_access",
  "application_type": "web"
}
```

The registration response includes a `registration_access_token` and `registration_client_uri`. Use that token as a Bearer token for the RFC 7592 endpoints. Successful `GET` and `PUT` operations rotate it, so clients must persist the newly returned value. For confidential clients using a shared secret, those operations also rotate `client_secret`. Client secrets and registration access tokens are stored as salted hashes and are returned in plaintext only when issued or rotated.

Registration and configuration requests require HTTPS by default. The accepted scopes, grants, authentication methods, HTTPS policy, and Initial Access Token scope can be configured through `OAuthAuthorizationServerOptions.ClientRegistration*` and `RequireHttpsForClientRegistration`.

To implement anonymous registration or another admission policy, replace `IOAuthInitialAccessTokenHandler`. The registration endpoints remain enabled; only the admission decision changes:

```csharp
using Microsoft.AspNetCore.Http;
using NOF.Contract;
using NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class AnonymousClientRegistrationHandler : IOAuthInitialAccessTokenHandler
{
    public Task<Result> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success());
}

builder.Services.AddScoped<IOAuthInitialAccessTokenHandler, AnonymousClientRegistrationHandler>();
```

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

Clients that authenticate with a signed JWT instead of a shared secret can register an RSA public JWKS:

```csharp
builder.AddOidcServer(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.AccessTokenAudience = "your-app";
    options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
})
.AddClientAssertionClient(
    "worker-client",
    """
    {"keys":[{"kty":"RSA","use":"sig","kid":"worker-key-1","alg":"RS256","n":"...","e":"AQAB"}]}
    """,
    ["api.read", "api.write"]);
```

The client posts `client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer` and the signed JWT as `client_assertion`. The assertion must use `RS256`, set both `iss` and `sub` to the client ID, use the published token endpoint URL as `aud`, and contain a valid `exp`. Its lifetime is limited by `ClientAssertionMaximumLifetime` (five minutes by default) with `ClientAssertionClockSkew` (one minute by default). When a `jti` is present, the built-in repository rejects replay for the assertion's remaining lifetime.

Authorization requests may omit `redirect_uri` when the client has exactly one registered redirect URI; in that case the server uses the registered value automatically. When `redirect_uri` is supplied, it must exactly match one of the client's registered `RedirectUris`. The default persisted client management service rejects non-absolute redirect URIs at create/update time, and the authorization endpoint refuses to redirect to unregistered callback URLs.

`ITokenService` accepts explicit multi-value claims through `TokenClaim.Array(...)`. The issuer expands those values into repeated same-name claims so the resulting JWT payload is emitted as a standard JSON array claim.

`ITokenService` also accepts explicit JSON object claims through `TokenClaim.Json(...)`. The default token-exchange handler emits the standard chained `act` claim for confidential clients, omits `act` for public clients, and by default issues client-credentials subjects in the form `client:{client_id}`.

## Device authorization flow

RFC 8628 device authorization is available through `POST /oauth2/device_authorization` and the existing
`POST /oauth2/token` endpoint. Device clients must explicitly register the
`urn:ietf:params:oauth:grant-type:device_code` grant; it is not added to existing public clients implicitly.

The selector provides a public-device-client shortcut:

```csharp
builder.AddOidcServer(options =>
{
    options.Issuer = "https://auth.example.com/oauth2";
    options.AccessTokenAudience = "your-app";
    options.SigningKeyEncryptionKey = "your-shared-signing-key-passphrase";
})
.AddDeviceClient(
    "living-room-tv",
    ["openid", "profile", "offline_access"],
    displayName: "Living Room TV");
```

The device authorization response contains `device_code`, `user_code`, `verification_uri`,
`verification_uri_complete`, `expires_in`, and `interval`. The client polls the token endpoint with:

```http
POST /oauth2/token HTTP/1.1
Content-Type: application/x-www-form-urlencoded

grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code&
device_code=<device-code>&
client_id=living-room-tv
```

NOF provides the protocol and distributed-cache state machine but does not prescribe an application login or
consent UI. Replace `IOAuthDeviceVerificationEndpoint` and use `IOAuthDeviceGrantService` to inspect and complete
the request:

```csharp
builder.Services.AddScoped<IOAuthDeviceVerificationEndpoint, YourDeviceVerificationEndpoint>();

var pending = await deviceGrantService.GetPendingAsync(userCode, cancellationToken);
await deviceGrantService.ApproveAsync(userCode, subject, cancellationToken);
// or: await deviceGrantService.DenyAsync(userCode, cancellationToken);
```

The verification endpoint must authenticate the user, protect state-changing form posts against CSRF, rate-limit
user-code attempts by authenticated user and network source, and clearly display the client, requested scopes, and
user code before approval. NOF adds no-store, clickjacking, and referrer-policy response headers to the mapped
verification route. Configure a shared cache rider for multi-instance deployments because device authorization
state and locks are stored through `ICacheService`.

Device defaults are configurable with `DeviceCodeExpiration`, `DevicePollingInterval`,
`DeviceUserCodeLength`, `DeviceVerificationUri`, `RedeemedDeviceCodeGracePeriod`,
`ExpiredDeviceCodeRetention`, and `RequireHttpsForDeviceAuthorization`.

## License

Apache-2.0
