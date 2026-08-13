using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class PersistenceOAuthClientRepository(
    IDbContext dbContext,
    ICacheService cacheService,
    IOptions<OAuthAuthorizationServerOptions> oauthOptions) : IOAuthClientRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<OAuthClientCredentialsValidationResult> ValidateClientCredentialsAsync(
        OAuthClientCredentialsValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client credentials are invalid.");
        }

        var client = await FindClientAsync(request.ClientId, cancellationToken).ConfigureAwait(false);
        if (client is null
            || !client.IsEnabled
            || client.ClientType != OAuthClientType.Confidential)
        {
            return new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client credentials are invalid.");
        }

        JwtSecurityToken? validatedClientAssertion = null;
        if (string.Equals(
                request.AuthenticationMethod,
                OAuthClientAuthenticationMethods.PrivateKeyJwt,
                StringComparison.Ordinal))
        {
            validatedClientAssertion = ValidateClientAssertion(client, request);
            if (validatedClientAssertion is null)
            {
                return new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client assertion is invalid.");
            }
        }
        else if (!IsClientSecretAuthenticationMethod(request.AuthenticationMethod)
                 || string.IsNullOrWhiteSpace(request.ClientSecret)
                 || string.IsNullOrWhiteSpace(client.SecretHash)
                 || !VerifySecret(request.ClientSecret, client.SecretSalt, client.SecretHash))
        {
            return new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client credentials are invalid.");
        }

        var allowedScopes = DeserializeScopes(client.AllowedScopes);
        var scopes = request.RequestedScopes.Count == 0
            ? allowedScopes
            : request.RequestedScopes.Where(scope => allowedScopes.Contains(scope)).ToHashSet(StringComparer.Ordinal);
        if (request.RequestedScopes.Count > 0 && scopes.Count != request.RequestedScopes.Count)
        {
            return new OAuthClientCredentialsValidationResult.Failure("invalid_scope", "requested scope is not allowed.");
        }

        if (validatedClientAssertion is not null
            && !await TryMarkClientAssertionAsUsedAsync(validatedClientAssertion, client.ClientId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client assertion has already been used.");
        }

        var claims = DeserializeClaims(client.AccessTokenClaims)
            .Select(static claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
            .ToList();
        if (claims.All(static claim => claim.Key != "client_id"))
        {
            claims.Insert(0, new KeyValuePair<string, string>("client_id", client.ClientId));
        }

        return new OAuthClientCredentialsValidationResult.Success(client.ClientId, scopes, claims);
    }

    public async Task<IReadOnlyList<OAuthClientDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        var clients = await dbContext
            .Set<OAuthClient>()
            .AsNoTracking()
            .OrderBy(static client => client.ClientId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return clients.Select(ToDescriptor).ToArray();
    }

    public async Task<Result<OAuthClientDescriptor>> GetAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Fail("invalid_request", "client_id is required.");
        }

        var client = await FindClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        return client is null
            ? Result.Fail("not_found", "OAuth client was not found.")
            : Result.Success(ToDescriptor(client));
    }

    public async Task<Result<OAuthClientSecretDescriptor>> CreateAsync(
        CreateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return Result.Fail("invalid_request", "client_id is required.");
        }

        if (await FindClientAsync(request.ClientId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Fail("conflict", "OAuth client already exists.");
        }

        var redirectUris = NormalizeRedirectUris(request.RedirectUris);
        if (redirectUris is null)
        {
            return Result.Fail("invalid_request", "redirect_uris must contain only absolute URIs.");
        }

        if (!TryNormalizeJsonWebKeySet(request.JsonWebKeySet, out var jsonWebKeySet))
        {
            return Result.Fail("invalid_request", "json_web_key_set must contain valid public RSA signing keys.");
        }

        if (request.ClientType == OAuthClientType.Public && !string.IsNullOrEmpty(jsonWebKeySet))
        {
            return Result.Fail("invalid_request", "public clients must not register client assertion keys.");
        }

        var (secret, salt, hash) = request.ClientType == OAuthClientType.Public
            || (!string.IsNullOrEmpty(jsonWebKeySet) && string.IsNullOrWhiteSpace(request.ClientSecret))
            ? (null, string.Empty, string.Empty)
            : CreateSecretMaterial(request.ClientSecret);
        var now = DateTime.UtcNow;
        var client = new OAuthClient
        {
            ClientId = request.ClientId.Trim(),
            DisplayName = NormalizeDisplayName(request.DisplayName, request.ClientId),
            SecretSalt = salt,
            SecretHash = hash,
            JsonWebKeySet = jsonWebKeySet,
            AllowedScopes = SerializeScopes(request.AllowedScopes),
            RedirectUris = SerializeRedirectUris(redirectUris),
            AccessTokenClaims = SerializeClaims(request.AccessTokenClaims),
            ClientType = request.ClientType,
            IsEnabled = request.IsEnabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await dbContext.Set<OAuthClient>().AddAsync(client, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new OAuthClientSecretDescriptor
        {
            Client = ToDescriptor(client),
            ClientSecret = secret
        });
    }

    public async Task<Result<OAuthClientDescriptor>> UpdateAsync(
        string clientId,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Fail("invalid_request", "client_id is required.");
        }

        var client = await FindClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return Result.Fail("not_found", "OAuth client was not found.");
        }

        var redirectUris = NormalizeRedirectUris(request.RedirectUris);
        if (redirectUris is null)
        {
            return Result.Fail("invalid_request", "redirect_uris must contain only absolute URIs.");
        }

        var jsonWebKeySet = client.JsonWebKeySet;
        if (request.JsonWebKeySet is not null
            && !TryNormalizeJsonWebKeySet(request.JsonWebKeySet, out jsonWebKeySet))
        {
            return Result.Fail("invalid_request", "json_web_key_set must contain valid public RSA signing keys.");
        }

        if (request.ClientType == OAuthClientType.Public && !string.IsNullOrEmpty(jsonWebKeySet))
        {
            return Result.Fail("invalid_request", "public clients must not register client assertion keys.");
        }

        client.DisplayName = NormalizeDisplayName(request.DisplayName, client.ClientId);
        client.AllowedScopes = SerializeScopes(request.AllowedScopes);
        client.RedirectUris = SerializeRedirectUris(redirectUris);
        client.AccessTokenClaims = SerializeClaims(request.AccessTokenClaims);
        client.ClientType = request.ClientType;
        if (client.ClientType == OAuthClientType.Public)
        {
            client.SecretSalt = string.Empty;
            client.SecretHash = string.Empty;
            jsonWebKeySet = string.Empty;
        }
        client.JsonWebKeySet = jsonWebKeySet;
        client.IsEnabled = request.IsEnabled;
        client.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToDescriptor(client));
    }

    public async Task<Result<OAuthClientSecretDescriptor>> RotateSecretAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Fail("invalid_request", "client_id is required.");
        }

        var client = await FindClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return Result.Fail("not_found", "OAuth client was not found.");
        }

        if (client.ClientType == OAuthClientType.Public)
        {
            return Result.Fail("invalid_operation", "public clients do not use client secrets.");
        }

        var (secret, salt, hash) = CreateSecretMaterial();
        client.SecretSalt = salt;
        client.SecretHash = hash;
        client.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new OAuthClientSecretDescriptor
        {
            Client = ToDescriptor(client),
            ClientSecret = secret
        });
    }

    public async Task<Result> DeleteAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Fail("invalid_request", "client_id is required.");
        }

        var client = await FindClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return Result.Fail("not_found", "OAuth client was not found.");
        }

        dbContext.Set<OAuthClient>().Remove(client);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<OAuthClient?> FindClientAsync(string clientId, CancellationToken cancellationToken)
    {
        return await dbContext
            .Set<OAuthClient>()
            .Where(client => client.ClientId == clientId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static OAuthClientDescriptor ToDescriptor(OAuthClient client)
    {
        return new OAuthClientDescriptor
        {
            ClientId = client.ClientId,
            DisplayName = client.DisplayName,
            AllowedScopes = DeserializeScopes(client.AllowedScopes).OrderBy(static scope => scope, StringComparer.Ordinal).ToArray(),
            RedirectUris = DeserializeRedirectUris(client.RedirectUris).OrderBy(static uri => uri, StringComparer.Ordinal).ToArray(),
            AccessTokenClaims = DeserializeClaims(client.AccessTokenClaims),
            JsonWebKeySet = client.JsonWebKeySet,
            ClientType = client.ClientType,
            IsEnabled = client.IsEnabled,
            CreatedAtUtc = client.CreatedAtUtc,
            UpdatedAtUtc = client.UpdatedAtUtc
        };
    }

    private static string NormalizeDisplayName(string displayName, string clientId)
        => string.IsNullOrWhiteSpace(displayName) ? clientId.Trim() : displayName.Trim();

    private static string SerializeScopes(IEnumerable<string> scopes)
        => JsonSerializer.Serialize(
            scopes
                .Where(static scope => !string.IsNullOrWhiteSpace(scope))
                .Select(static scope => scope.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static scope => scope, StringComparer.Ordinal)
                .ToArray(),
            JsonOptions);

    private static IReadOnlySet<string> DeserializeScopes(string scopes)
        => (JsonSerializer.Deserialize<string[]>(scopes, JsonOptions) ?? [])
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .ToHashSet(StringComparer.Ordinal);

    private static string SerializeRedirectUris(IEnumerable<string> redirectUris)
        => JsonSerializer.Serialize(
            redirectUris
                .Where(static redirectUri => !string.IsNullOrWhiteSpace(redirectUri))
                .Select(static redirectUri => redirectUri.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static redirectUri => redirectUri, StringComparer.Ordinal)
                .ToArray(),
            JsonOptions);

    private static IReadOnlySet<string> DeserializeRedirectUris(string redirectUris)
    {
        if (string.IsNullOrWhiteSpace(redirectUris))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return (JsonSerializer.Deserialize<string[]>(redirectUris, JsonOptions) ?? [])
            .Where(static redirectUri => !string.IsNullOrWhiteSpace(redirectUri))
            .Select(static redirectUri => redirectUri.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string>? NormalizeRedirectUris(IEnumerable<string> redirectUris)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var redirectUri in redirectUris)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                continue;
            }

            var trimmed = redirectUri.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out _))
            {
                return null;
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    private static string SerializeClaims(IEnumerable<OAuthClientClaim> claims)
        => JsonSerializer.Serialize(
            claims
                .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type) && claim.Value is not null)
                .Select(static claim => new OAuthClientClaim(claim.Type.Trim(), claim.Value))
                .ToArray(),
            JsonOptions);

    private static IReadOnlyList<OAuthClientClaim> DeserializeClaims(string claims)
        => JsonSerializer.Deserialize<OAuthClientClaim[]>(claims, JsonOptions) ?? [];

    private JwtSecurityToken? ValidateClientAssertion(
        OAuthClient client,
        OAuthClientCredentialsValidationRequest request)
    {
        if (!string.Equals(request.ClientAssertionType, OAuthClientAssertionTypes.JwtBearer, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.ClientAssertion)
            || !string.IsNullOrWhiteSpace(request.ClientSecret)
            || string.IsNullOrWhiteSpace(request.Audience)
            || string.IsNullOrWhiteSpace(client.JsonWebKeySet))
        {
            return null;
        }

        try
        {
            var keySet = new JsonWebKeySet(client.JsonWebKeySet);
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            handler.ValidateToken(
                request.ClientAssertion,
                new TokenValidationParameters
                {
                    ClockSkew = oauthOptions.Value.ClientAssertionClockSkew,
                    IssuerSigningKeys = keySet.Keys,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidAudience = request.Audience,
                    ValidIssuer = client.ClientId,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true
                },
                out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt
                || !string.Equals(jwt.Subject, client.ClientId, StringComparison.Ordinal)
                || jwt.Payload.Expiration is not long expiresAt)
            {
                return null;
            }

            var expiration = DateTimeOffset.FromUnixTimeSeconds(expiresAt);
            var maximumExpiration = DateTimeOffset.UtcNow
                .Add(oauthOptions.Value.ClientAssertionMaximumLifetime)
                .Add(oauthOptions.Value.ClientAssertionClockSkew);
            return expiration <= maximumExpiration ? jwt : null;
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            return null;
        }
    }

    private async ValueTask<bool> TryMarkClientAssertionAsUsedAsync(
        JwtSecurityToken clientAssertion,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientAssertion.Id))
        {
            return true;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(clientAssertion.Payload.Expiration!.Value)
            .Add(oauthOptions.Value.ClientAssertionClockSkew);
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var replayIdentifier = Base64UrlEncode(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{clientId}\n{clientAssertion.Id}")));
        return await cacheService.SetIfNotExistsAsync(
            $"oidc:client_assertion:{replayIdentifier}",
            true,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsClientSecretAuthenticationMethod(string authenticationMethod)
        => string.Equals(
               authenticationMethod,
               OAuthClientAuthenticationMethods.ClientSecretBasic,
               StringComparison.Ordinal)
           || string.Equals(
               authenticationMethod,
               OAuthClientAuthenticationMethods.ClientSecretPost,
               StringComparison.Ordinal);

    private static bool TryNormalizeJsonWebKeySet(string? jsonWebKeySet, out string normalizedJsonWebKeySet)
    {
        normalizedJsonWebKeySet = string.Empty;
        if (string.IsNullOrWhiteSpace(jsonWebKeySet))
        {
            return true;
        }

        try
        {
            var keySet = new JsonWebKeySet(jsonWebKeySet);
            if (keySet.Keys.Count == 0
                || keySet.Keys.Any(static key =>
                    !string.Equals(key.Kty, JsonWebAlgorithmsKeyTypes.RSA, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(key.N)
                    || string.IsNullOrWhiteSpace(key.E)
                    || !string.IsNullOrWhiteSpace(key.D)
                    || !string.IsNullOrWhiteSpace(key.DP)
                    || !string.IsNullOrWhiteSpace(key.DQ)
                    || key.Oth.Count > 0
                    || !string.IsNullOrWhiteSpace(key.P)
                    || !string.IsNullOrWhiteSpace(key.Q)
                    || !string.IsNullOrWhiteSpace(key.QI)
                    || (!string.IsNullOrWhiteSpace(key.Use)
                        && !string.Equals(key.Use, JsonWebKeyUseNames.Sig, StringComparison.Ordinal))
                    || (!string.IsNullOrWhiteSpace(key.Alg)
                        && !string.Equals(key.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))))
            {
                return false;
            }

            if (keySet.Keys.Count > 1
                && (keySet.Keys.Any(static key => string.IsNullOrWhiteSpace(key.Kid))
                    || keySet.Keys.Select(static key => key.Kid).Distinct(StringComparer.Ordinal).Count() != keySet.Keys.Count))
            {
                return false;
            }

            normalizedJsonWebKeySet = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<JsonElement>(jsonWebKeySet),
                JsonOptions);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return false;
        }
    }

    private static (string Secret, string Salt, string Hash) CreateSecretMaterial(string? clientSecret = null)
    {
        var secret = string.IsNullOrWhiteSpace(clientSecret)
            ? $"nof_{Base64UrlEncode(RandomNumberGenerator.GetBytes(32))}"
            : clientSecret.Trim();
        var salt = Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        return (secret, salt, HashSecret(secret, salt));
    }

    private static string HashSecret(string secret, string salt)
        => Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}.{secret}")));

    private static bool VerifySecret(string secret, string salt, string expectedHash)
    {
        var actualBytes = Encoding.UTF8.GetBytes(HashSecret(secret, salt));
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
