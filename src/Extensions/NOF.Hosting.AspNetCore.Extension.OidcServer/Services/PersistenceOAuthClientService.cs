using NOF.Application;
using NOF.Contract;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class PersistenceOAuthClientService(IDbContext dbContext) : IOAuthClientManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<OAuthClientCredentialsValidationResult> ValidateClientCredentialsAsync(
        OAuthClientCredentialsValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client credentials are invalid.");
        }

        var client = await FindClientAsync(request.ClientId, cancellationToken).ConfigureAwait(false);
        if (client is null
            || !client.IsEnabled
            || client.ClientType != OAuthClientType.Confidential
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

        var (secret, salt, hash) = request.ClientType == OAuthClientType.Public
            ? (null, string.Empty, string.Empty)
            : CreateSecretMaterial(request.ClientSecret);
        var now = DateTime.UtcNow;
        var client = new OAuthClient
        {
            ClientId = request.ClientId.Trim(),
            DisplayName = NormalizeDisplayName(request.DisplayName, request.ClientId),
            SecretSalt = salt,
            SecretHash = hash,
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

        client.DisplayName = NormalizeDisplayName(request.DisplayName, client.ClientId);
        client.AllowedScopes = SerializeScopes(request.AllowedScopes);
        client.RedirectUris = SerializeRedirectUris(redirectUris);
        client.AccessTokenClaims = SerializeClaims(request.AccessTokenClaims);
        client.ClientType = request.ClientType;
        if (client.ClientType == OAuthClientType.Public)
        {
            client.SecretSalt = string.Empty;
            client.SecretHash = string.Empty;
        }
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
