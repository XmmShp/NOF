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
    IOptions<OAuthAuthorizationServerOptions> oauthOptions) : IOAuthClientRepository, IOAuthClientRegistrationRepository
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

        if (!IsAuthenticationMethodRegistered(client, request.AuthenticationMethod))
        {
            return new OAuthClientCredentialsValidationResult.Failure("invalid_client", "client authentication method is not registered.");
        }

        if (!string.IsNullOrWhiteSpace(request.GrantType)
            && !DeserializeGrantTypes(client).Contains(request.GrantType))
        {
            return new OAuthClientCredentialsValidationResult.Failure("unauthorized_client", "grant_type is not allowed for this client.");
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
        => await CreateAsync(request, null, cancellationToken).ConfigureAwait(false);

    private async Task<Result<OAuthClientSecretDescriptor>> CreateAsync(
        CreateOAuthClientRequest request,
        (string Salt, string Hash)? registrationAccessToken,
        CancellationToken cancellationToken)
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

        var authenticationMethod = NormalizeAuthenticationMethod(
            request.TokenEndpointAuthenticationMethod,
            request.ClientType,
            jsonWebKeySet);
        var authenticationError = ValidateAuthenticationMethod(
            authenticationMethod,
            request.ClientType,
            jsonWebKeySet);
        if (authenticationError is not null)
        {
            return Result.Fail("invalid_request", authenticationError);
        }

        var grantTypes = NormalizeGrantTypes(request.AllowedGrantTypes, request.ClientType);
        var responseTypes = NormalizeResponseTypes(request.AllowedResponseTypes, grantTypes);
        if (request.ClientType == OAuthClientType.Public && grantTypes.Contains(OAuthGrantTypes.ClientCredentials))
        {
            return Result.Fail("invalid_request", "public clients must not use the client_credentials grant type.");
        }
        var protocolError = request.AllowedGrantTypes.Count > 0 || request.AllowedResponseTypes.Count > 0
            ? ValidateProtocolMetadata(grantTypes, responseTypes, redirectUris)
            : null;
        if (protocolError is not null)
        {
            return Result.Fail("invalid_request", protocolError);
        }

        if (!IsSupportedApplicationType(request.ApplicationType))
        {
            return Result.Fail("invalid_request", "application_type is not supported.");
        }

        var (secret, salt, hash) = request.ClientType == OAuthClientType.Public
            || string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.PrivateKeyJwt, StringComparison.Ordinal)
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
            TokenEndpointAuthenticationMethod = string.IsNullOrWhiteSpace(request.TokenEndpointAuthenticationMethod)
                ? string.Empty
                : authenticationMethod,
            AllowedGrantTypes = SerializeStrings(grantTypes),
            AllowedResponseTypes = SerializeStrings(responseTypes),
            ApplicationType = request.ApplicationType.Trim(),
            RegistrationMetadata = SerializeRegistrationMetadata(request),
            RegistrationAccessTokenSalt = registrationAccessToken?.Salt ?? string.Empty,
            RegistrationAccessTokenHash = registrationAccessToken?.Hash ?? string.Empty,
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

        var authenticationMethod = NormalizeAuthenticationMethod(
            request.TokenEndpointAuthenticationMethod,
            request.ClientType,
            jsonWebKeySet);
        var authenticationError = ValidateAuthenticationMethod(
            authenticationMethod,
            request.ClientType,
            jsonWebKeySet);
        if (authenticationError is not null)
        {
            return Result.Fail("invalid_request", authenticationError);
        }

        if (request.ClientType == OAuthClientType.Confidential
            && IsClientSecretAuthenticationMethod(authenticationMethod)
            && string.IsNullOrWhiteSpace(client.SecretHash))
        {
            return Result.Fail("invalid_operation", "rotate the client secret before selecting a client secret authentication method.");
        }

        var grantTypes = NormalizeGrantTypes(request.AllowedGrantTypes, request.ClientType);
        var responseTypes = NormalizeResponseTypes(request.AllowedResponseTypes, grantTypes);
        if (request.ClientType == OAuthClientType.Public && grantTypes.Contains(OAuthGrantTypes.ClientCredentials))
        {
            return Result.Fail("invalid_request", "public clients must not use the client_credentials grant type.");
        }
        var protocolError = request.AllowedGrantTypes.Count > 0 || request.AllowedResponseTypes.Count > 0
            ? ValidateProtocolMetadata(grantTypes, responseTypes, redirectUris)
            : null;
        if (protocolError is not null)
        {
            return Result.Fail("invalid_request", protocolError);
        }

        if (!IsSupportedApplicationType(request.ApplicationType))
        {
            return Result.Fail("invalid_request", "application_type is not supported.");
        }

        client.DisplayName = NormalizeDisplayName(request.DisplayName, client.ClientId);
        client.AllowedScopes = SerializeScopes(request.AllowedScopes);
        client.RedirectUris = SerializeRedirectUris(redirectUris);
        client.AccessTokenClaims = SerializeClaims(request.AccessTokenClaims);
        client.TokenEndpointAuthenticationMethod = string.IsNullOrWhiteSpace(request.TokenEndpointAuthenticationMethod)
            ? string.Empty
            : authenticationMethod;
        client.AllowedGrantTypes = SerializeStrings(grantTypes);
        client.AllowedResponseTypes = SerializeStrings(responseTypes);
        client.ApplicationType = request.ApplicationType.Trim();
        client.RegistrationMetadata = SerializeRegistrationMetadata(request);
        client.ClientType = request.ClientType;
        if (client.ClientType == OAuthClientType.Public)
        {
            client.SecretSalt = string.Empty;
            client.SecretHash = string.Empty;
            jsonWebKeySet = string.Empty;
        }
        else if (string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.PrivateKeyJwt, StringComparison.Ordinal))
        {
            client.SecretSalt = string.Empty;
            client.SecretHash = string.Empty;
        }
        client.JsonWebKeySet = jsonWebKeySet;
        client.IsEnabled = request.IsEnabled;
        client.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToDescriptor(client));
    }

    public async Task<Result<OAuthClientRegistrationSecretDescriptor>> RegisterAsync(
        CreateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var (registrationToken, registrationSalt, registrationHash) = CreateRegistrationAccessTokenMaterial();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var clientId = $"nof_dcr_{Base64UrlEncode(RandomNumberGenerator.GetBytes(24))}";
            var createResult = await CreateAsync(
                request with { ClientId = clientId },
                (registrationSalt, registrationHash),
                cancellationToken).ConfigureAwait(false);
            if (createResult.IsSuccess)
            {
                return Result.Success(new OAuthClientRegistrationSecretDescriptor
                {
                    Client = createResult.Value.Client,
                    ClientSecret = createResult.Value.ClientSecret,
                    RegistrationAccessToken = registrationToken
                });
            }

            if (!string.Equals(createResult.ErrorCode, "conflict", StringComparison.Ordinal))
            {
                return Result.Fail(createResult.ErrorCode, createResult.Message);
            }
        }

        return Result.Fail("server_error", "unable to allocate a unique client_id.");
    }

    public async Task<Result<OAuthClientRegistrationSecretDescriptor>> GetAsync(
        string clientId,
        string registrationAccessToken,
        CancellationToken cancellationToken = default)
    {
        await using var registrationLock = await cacheService.AcquireLockAsync(
            $"oidc:client-registration:{clientId}",
            TimeSpan.FromSeconds(30),
            cancellationToken).ConfigureAwait(false);
        var client = await FindClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null || !ValidateRegistrationAccessToken(client, registrationAccessToken))
        {
            return Result.Fail("invalid_token", "registration access token is invalid.");
        }

        var (rotatedToken, salt, hash) = CreateRegistrationAccessTokenMaterial();
        client.RegistrationAccessTokenSalt = salt;
        client.RegistrationAccessTokenHash = hash;
        string? rotatedClientSecret = null;
        if (client.ClientType == OAuthClientType.Confidential
            && IsClientSecretAuthenticationMethod(ResolveTokenEndpointAuthenticationMethod(client)))
        {
            var secretMaterial = CreateSecretMaterial();
            rotatedClientSecret = secretMaterial.Secret;
            client.SecretSalt = secretMaterial.Salt;
            client.SecretHash = secretMaterial.Hash;
        }
        client.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new OAuthClientRegistrationSecretDescriptor
        {
            Client = ToDescriptor(client),
            ClientSecret = rotatedClientSecret,
            RegistrationAccessToken = rotatedToken
        });
    }

    public async Task<Result<OAuthClientRegistrationSecretDescriptor>> UpdateAsync(
        string clientId,
        string registrationAccessToken,
        string? currentClientSecret,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var registrationLock = await cacheService.AcquireLockAsync(
            $"oidc:client-registration:{clientId}",
            TimeSpan.FromSeconds(30),
            cancellationToken).ConfigureAwait(false);
        var client = await FindClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null || !ValidateRegistrationAccessToken(client, registrationAccessToken))
        {
            return Result.Fail("invalid_token", "registration access token is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(currentClientSecret)
            && (string.IsNullOrWhiteSpace(client.SecretHash)
                || !VerifySecret(currentClientSecret, client.SecretSalt, client.SecretHash)))
        {
            return Result.Fail("invalid_client_metadata", "client_secret does not match the currently issued secret.");
        }

        var redirectUris = NormalizeRedirectUris(request.RedirectUris);
        if (redirectUris is null)
        {
            return Result.Fail("invalid_redirect_uri", "redirect_uris must contain only valid absolute URIs.");
        }

        if (!TryNormalizeJsonWebKeySet(request.JsonWebKeySet, out var jsonWebKeySet))
        {
            return Result.Fail("invalid_client_metadata", "jwks must contain valid public RSA signing keys.");
        }

        if (request.ClientType == OAuthClientType.Public && !string.IsNullOrEmpty(jsonWebKeySet))
        {
            return Result.Fail("invalid_client_metadata", "public clients must not register client assertion keys.");
        }

        var authenticationMethod = NormalizeAuthenticationMethod(
            request.TokenEndpointAuthenticationMethod,
            request.ClientType,
            jsonWebKeySet);
        var authenticationError = ValidateAuthenticationMethod(
            authenticationMethod,
            request.ClientType,
            jsonWebKeySet);
        if (authenticationError is not null)
        {
            return Result.Fail("invalid_client_metadata", authenticationError);
        }

        var grantTypes = NormalizeGrantTypes(request.AllowedGrantTypes, request.ClientType);
        var responseTypes = NormalizeResponseTypes(request.AllowedResponseTypes, grantTypes);
        if (request.ClientType == OAuthClientType.Public && grantTypes.Contains(OAuthGrantTypes.ClientCredentials))
        {
            return Result.Fail("invalid_client_metadata", "public clients must not use the client_credentials grant type.");
        }
        var protocolError = ValidateProtocolMetadata(grantTypes, responseTypes, redirectUris);
        if (protocolError is not null)
        {
            return Result.Fail("invalid_client_metadata", protocolError);
        }

        if (!IsSupportedApplicationType(request.ApplicationType))
        {
            return Result.Fail("invalid_client_metadata", "application_type is not supported.");
        }

        string? newClientSecret = null;
        if (request.ClientType == OAuthClientType.Public
            || string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.PrivateKeyJwt, StringComparison.Ordinal))
        {
            client.SecretSalt = string.Empty;
            client.SecretHash = string.Empty;
        }
        else
        {
            var secretMaterial = CreateSecretMaterial();
            newClientSecret = secretMaterial.Secret;
            client.SecretSalt = secretMaterial.Salt;
            client.SecretHash = secretMaterial.Hash;
        }

        client.DisplayName = NormalizeDisplayName(request.DisplayName, client.ClientId);
        client.AllowedScopes = SerializeScopes(request.AllowedScopes);
        client.RedirectUris = SerializeRedirectUris(redirectUris);
        client.AccessTokenClaims = "[]";
        client.JsonWebKeySet = jsonWebKeySet;
        client.TokenEndpointAuthenticationMethod = authenticationMethod;
        client.AllowedGrantTypes = SerializeStrings(grantTypes);
        client.AllowedResponseTypes = SerializeStrings(responseTypes);
        client.ApplicationType = request.ApplicationType.Trim();
        client.RegistrationMetadata = SerializeRegistrationMetadata(request);
        client.ClientType = request.ClientType;
        client.IsEnabled = true;
        client.UpdatedAtUtc = DateTime.UtcNow;

        var (rotatedToken, registrationSalt, registrationHash) = CreateRegistrationAccessTokenMaterial();
        client.RegistrationAccessTokenSalt = registrationSalt;
        client.RegistrationAccessTokenHash = registrationHash;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new OAuthClientRegistrationSecretDescriptor
        {
            Client = ToDescriptor(client),
            ClientSecret = newClientSecret,
            RegistrationAccessToken = rotatedToken
        });
    }

    public async Task<Result> DeleteAsync(
        string clientId,
        string registrationAccessToken,
        CancellationToken cancellationToken = default)
    {
        await using var registrationLock = await cacheService.AcquireLockAsync(
            $"oidc:client-registration:{clientId}",
            TimeSpan.FromSeconds(30),
            cancellationToken).ConfigureAwait(false);
        var client = await FindClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null || !ValidateRegistrationAccessToken(client, registrationAccessToken))
        {
            return Result.Fail("invalid_token", "registration access token is invalid.");
        }

        dbContext.Set<OAuthClient>().Remove(client);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
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
        var registrationMetadata = DeserializeRegistrationMetadata(client.RegistrationMetadata);
        return new OAuthClientDescriptor
        {
            ClientId = client.ClientId,
            DisplayName = client.DisplayName,
            AllowedScopes = DeserializeScopes(client.AllowedScopes).OrderBy(static scope => scope, StringComparer.Ordinal).ToArray(),
            RedirectUris = DeserializeRedirectUris(client.RedirectUris).OrderBy(static uri => uri, StringComparer.Ordinal).ToArray(),
            AccessTokenClaims = DeserializeClaims(client.AccessTokenClaims),
            JsonWebKeySet = client.JsonWebKeySet,
            TokenEndpointAuthenticationMethod = ResolveTokenEndpointAuthenticationMethod(client),
            AllowedGrantTypes = DeserializeGrantTypes(client).OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            AllowedResponseTypes = DeserializeResponseTypes(client).OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            ApplicationType = string.IsNullOrWhiteSpace(client.ApplicationType)
                ? OAuthClientApplicationTypes.Web
                : client.ApplicationType,
            SubjectType = registrationMetadata.SubjectType,
            IdTokenSignedResponseAlgorithm = registrationMetadata.IdTokenSignedResponseAlgorithm,
            ClientUri = registrationMetadata.ClientUri,
            LogoUri = registrationMetadata.LogoUri,
            Contacts = registrationMetadata.Contacts,
            TermsOfServiceUri = registrationMetadata.TermsOfServiceUri,
            PolicyUri = registrationMetadata.PolicyUri,
            SoftwareId = registrationMetadata.SoftwareId,
            SoftwareVersion = registrationMetadata.SoftwareVersion,
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

    private static string SerializeStrings(IEnumerable<string> values)
        => JsonSerializer.Serialize(
            values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray(),
            JsonOptions);

    private static IReadOnlySet<string> DeserializeStrings(string values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return (JsonSerializer.Deserialize<string[]>(values, JsonOptions) ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> DeserializeGrantTypes(OAuthClient client)
    {
        var grantTypes = DeserializeStrings(client.AllowedGrantTypes);
        return grantTypes.Count > 0
            ? grantTypes
            : NormalizeGrantTypes([], client.ClientType);
    }

    private static IReadOnlySet<string> DeserializeResponseTypes(OAuthClient client)
    {
        var responseTypes = DeserializeStrings(client.AllowedResponseTypes);
        return responseTypes.Count > 0
            ? responseTypes
            : NormalizeResponseTypes([], DeserializeGrantTypes(client));
    }

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
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || !string.IsNullOrEmpty(uri.Fragment))
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

    private static string NormalizeAuthenticationMethod(
        string? authenticationMethod,
        OAuthClientType clientType,
        string jsonWebKeySet)
    {
        if (!string.IsNullOrWhiteSpace(authenticationMethod))
        {
            return authenticationMethod.Trim();
        }

        if (clientType == OAuthClientType.Public)
        {
            return OAuthClientAuthenticationMethods.None;
        }

        return string.IsNullOrWhiteSpace(jsonWebKeySet)
            ? OAuthClientAuthenticationMethods.ClientSecretBasic
            : OAuthClientAuthenticationMethods.PrivateKeyJwt;
    }

    private static string ResolveTokenEndpointAuthenticationMethod(OAuthClient client)
        => NormalizeAuthenticationMethod(
            client.TokenEndpointAuthenticationMethod,
            client.ClientType,
            client.JsonWebKeySet);

    private static bool IsAuthenticationMethodRegistered(OAuthClient client, string authenticationMethod)
    {
        if (!string.IsNullOrWhiteSpace(client.TokenEndpointAuthenticationMethod))
        {
            return string.Equals(
                authenticationMethod,
                client.TokenEndpointAuthenticationMethod,
                StringComparison.Ordinal);
        }

        var resolvedAuthenticationMethod = ResolveTokenEndpointAuthenticationMethod(client);
        return IsClientSecretAuthenticationMethod(resolvedAuthenticationMethod)
            ? IsClientSecretAuthenticationMethod(authenticationMethod)
            : string.Equals(authenticationMethod, resolvedAuthenticationMethod, StringComparison.Ordinal);
    }

    private static string? ValidateAuthenticationMethod(
        string authenticationMethod,
        OAuthClientType clientType,
        string jsonWebKeySet)
    {
        if (clientType == OAuthClientType.Public)
        {
            return string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.None, StringComparison.Ordinal)
                ? null
                : "public clients must use token_endpoint_auth_method=none.";
        }

        if (string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.None, StringComparison.Ordinal))
        {
            return "confidential clients must authenticate at the token endpoint.";
        }

        if (string.Equals(authenticationMethod, OAuthClientAuthenticationMethods.PrivateKeyJwt, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(jsonWebKeySet)
                ? "private_key_jwt clients must register jwks."
                : null;
        }

        return IsClientSecretAuthenticationMethod(authenticationMethod)
            ? null
            : "token_endpoint_auth_method is not supported.";
    }

    private static IReadOnlySet<string> NormalizeGrantTypes(
        IEnumerable<string> grantTypes,
        OAuthClientType clientType)
    {
        var normalized = grantTypes
            .Where(static grantType => !string.IsNullOrWhiteSpace(grantType))
            .Select(static grantType => grantType.Trim())
            .ToHashSet(StringComparer.Ordinal);
        if (normalized.Count > 0)
        {
            return normalized;
        }

        return clientType == OAuthClientType.Public
            ? new HashSet<string>(
                [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken, OAuthGrantTypes.TokenExchange],
                StringComparer.Ordinal)
            : new HashSet<string>(
                [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken, OAuthGrantTypes.ClientCredentials, OAuthGrantTypes.TokenExchange],
                StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> NormalizeResponseTypes(
        IEnumerable<string> responseTypes,
        IReadOnlySet<string> grantTypes)
    {
        var normalized = responseTypes
            .Where(static responseType => !string.IsNullOrWhiteSpace(responseType))
            .Select(static responseType => responseType.Trim())
            .ToHashSet(StringComparer.Ordinal);
        if (normalized.Count > 0)
        {
            return normalized;
        }

        return grantTypes.Contains(OAuthGrantTypes.AuthorizationCode)
            ? new HashSet<string>(["code"], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private static string? ValidateProtocolMetadata(
        IReadOnlySet<string> grantTypes,
        IReadOnlySet<string> responseTypes,
        IReadOnlySet<string> redirectUris)
    {
        var supportedGrantTypes = new HashSet<string>(
            [
                OAuthGrantTypes.AuthorizationCode,
                OAuthGrantTypes.RefreshToken,
                OAuthGrantTypes.ClientCredentials,
                OAuthGrantTypes.DeviceCode,
                OAuthGrantTypes.TokenExchange
            ],
            StringComparer.Ordinal);
        if (grantTypes.Any(grantType => !supportedGrantTypes.Contains(grantType)))
        {
            return "grant_types contains an unsupported value.";
        }

        if (responseTypes.Any(static responseType => !string.Equals(responseType, "code", StringComparison.Ordinal)))
        {
            return "response_types contains an unsupported value.";
        }

        if (grantTypes.Contains(OAuthGrantTypes.AuthorizationCode) != responseTypes.Contains("code"))
        {
            return "authorization_code and response_type=code must be registered together.";
        }

        if (grantTypes.Contains(OAuthGrantTypes.AuthorizationCode) && redirectUris.Count == 0)
        {
            return "redirect_uris is required for authorization_code clients.";
        }

        return null;
    }

    private static bool IsSupportedApplicationType(string applicationType)
        => string.Equals(applicationType, OAuthClientApplicationTypes.Web, StringComparison.Ordinal)
           || string.Equals(applicationType, OAuthClientApplicationTypes.Native, StringComparison.Ordinal);

    private static bool ValidateRegistrationAccessToken(OAuthClient? client, string registrationAccessToken)
        => client is not null
           && !string.IsNullOrWhiteSpace(registrationAccessToken)
           && !string.IsNullOrWhiteSpace(client.RegistrationAccessTokenHash)
           && VerifySecret(
               registrationAccessToken.Trim(),
               client.RegistrationAccessTokenSalt,
               client.RegistrationAccessTokenHash);

    private static (string Token, string Salt, string Hash) CreateRegistrationAccessTokenMaterial()
    {
        var token = $"nof_reg_{Base64UrlEncode(RandomNumberGenerator.GetBytes(32))}";
        var salt = Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        return (token, salt, HashSecret(token, salt));
    }

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SerializeRegistrationMetadata(CreateOAuthClientRequest request)
        => SerializeRegistrationMetadata(
            request.SubjectType,
            request.IdTokenSignedResponseAlgorithm,
            request.ClientUri,
            request.LogoUri,
            request.Contacts,
            request.TermsOfServiceUri,
            request.PolicyUri,
            request.SoftwareId,
            request.SoftwareVersion);

    private static string SerializeRegistrationMetadata(UpdateOAuthClientRequest request)
        => SerializeRegistrationMetadata(
            request.SubjectType,
            request.IdTokenSignedResponseAlgorithm,
            request.ClientUri,
            request.LogoUri,
            request.Contacts,
            request.TermsOfServiceUri,
            request.PolicyUri,
            request.SoftwareId,
            request.SoftwareVersion);

    private static string SerializeRegistrationMetadata(
        string subjectType,
        string idTokenSignedResponseAlgorithm,
        string? clientUri,
        string? logoUri,
        IReadOnlyList<string> contacts,
        string? termsOfServiceUri,
        string? policyUri,
        string? softwareId,
        string? softwareVersion)
        => JsonSerializer.Serialize(
            new OAuthClientStoredRegistrationMetadata
            {
                SubjectType = subjectType,
                IdTokenSignedResponseAlgorithm = idTokenSignedResponseAlgorithm,
                ClientUri = EmptyToNull(clientUri),
                LogoUri = EmptyToNull(logoUri),
                Contacts = contacts
                    .Where(static contact => !string.IsNullOrWhiteSpace(contact))
                    .Select(static contact => contact.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                TermsOfServiceUri = EmptyToNull(termsOfServiceUri),
                PolicyUri = EmptyToNull(policyUri),
                SoftwareId = EmptyToNull(softwareId),
                SoftwareVersion = EmptyToNull(softwareVersion)
            },
            JsonOptions);

    private static OAuthClientStoredRegistrationMetadata DeserializeRegistrationMetadata(string metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return new OAuthClientStoredRegistrationMetadata();
        }

        try
        {
            return JsonSerializer.Deserialize<OAuthClientStoredRegistrationMetadata>(metadata, JsonOptions)
                ?? new OAuthClientStoredRegistrationMetadata();
        }
        catch (JsonException)
        {
            return new OAuthClientStoredRegistrationMetadata();
        }
    }

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
