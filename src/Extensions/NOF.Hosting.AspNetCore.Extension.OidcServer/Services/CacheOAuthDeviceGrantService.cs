using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class CacheOAuthDeviceGrantService(
    ICacheService cacheService,
    IServiceProvider serviceProvider,
    IOptions<OAuthAuthorizationServerOptions> options,
    TimeProvider timeProvider) : IOAuthDeviceGrantService
{
    private const string UserCodeAlphabet = "BCDFGHJKLMNPQRSTVWXZ";
    private static readonly TimeSpan LockExpiration = TimeSpan.FromSeconds(30);

    public async Task<Result<OAuthDeviceAuthorizationResponse>> CreateAsync(
        CreateOAuthDeviceGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return Result.Fail("invalid_client", "client_id is required.");
        }

        var serverOptions = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now.Add(serverOptions.DeviceCodeExpiration);
        var pollingIntervalSeconds = checked((int)Math.Ceiling(serverOptions.DevicePollingInterval.TotalSeconds));
        var verificationUri = ResolveVerificationUri(serverOptions);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var deviceCode = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var deviceCodeDigest = HashValue(deviceCode);
            var userCode = CreateUserCode(serverOptions.DeviceUserCodeLength);
            var normalizedUserCode = NormalizeUserCode(userCode);
            var userCodeKey = new OidcDeviceUserCodeCacheKey(HashValue(normalizedUserCode));
            var deviceCodeKey = new OidcDeviceAuthorizationCacheKey(deviceCodeDigest);
            var session = new OidcDeviceAuthorizationCacheValue
            {
                UserCode = userCode,
                ClientId = request.ClientId.Trim(),
                ClientDisplayName = string.IsNullOrWhiteSpace(request.ClientDisplayName)
                    ? request.ClientId.Trim()
                    : request.ClientDisplayName.Trim(),
                ClientLogoUri = string.IsNullOrWhiteSpace(request.ClientLogoUri) ? null : request.ClientLogoUri.Trim(),
                Scope = string.Join(' ', request.Scopes.OrderBy(static value => value, StringComparer.Ordinal)),
                Status = OidcDeviceAuthorizationStatus.Pending,
                CreatedAtUtc = now,
                ExpiresAtUtc = expiresAtUtc,
                PollingIntervalSeconds = pollingIntervalSeconds,
                NextAllowedPollAtUtc = now.AddSeconds(pollingIntervalSeconds)
            };

            var userCodeReserved = await cacheService.SetIfNotExistsAsync(
                userCodeKey,
                deviceCodeDigest,
                new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAtUtc },
                cancellationToken).ConfigureAwait(false);
            if (!userCodeReserved)
            {
                continue;
            }

            var deviceCodeReserved = await cacheService.SetIfNotExistsAsync(
                deviceCodeKey,
                session,
                CreateSessionCacheOptions(session),
                cancellationToken).ConfigureAwait(false);
            if (!deviceCodeReserved)
            {
                await cacheService.RemoveAsync(userCodeKey, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return Result.Success(new OAuthDeviceAuthorizationResponse
            {
                DeviceCode = deviceCode,
                UserCode = userCode,
                VerificationUri = verificationUri,
                VerificationUriComplete = AddQueryString(verificationUri, "user_code", userCode),
                ExpiresIn = checked((long)Math.Ceiling(serverOptions.DeviceCodeExpiration.TotalSeconds)),
                Interval = pollingIntervalSeconds
            });
        }

        return Result.Fail("server_error", "Unable to allocate a unique device authorization code.");
    }

    public async Task<Result<OAuthDeviceAuthorizationDescriptor>> GetPendingAsync(
        string userCode,
        CancellationToken cancellationToken = default)
    {
        var sessionLookup = await ResolveSessionByUserCodeAsync(userCode, cancellationToken).ConfigureAwait(false);
        if (!sessionLookup.IsSuccess)
        {
            return Result.Fail(sessionLookup.ErrorCode, sessionLookup.Message);
        }

        var (userCodeKey, deviceCodeKey) = sessionLookup.Value;
        await using var sessionLock = await cacheService
            .AcquireLockAsync(deviceCodeKey, LockExpiration, cancellationToken)
            .ConfigureAwait(false);
        var sessionValue = await cacheService.GetAsync(deviceCodeKey, cancellationToken).ConfigureAwait(false);
        if (!sessionValue.HasValue)
        {
            await cacheService.RemoveAsync(userCodeKey, cancellationToken).ConfigureAwait(false);
            return Result.Fail("invalid_user_code", "user_code is invalid or expired.");
        }

        var session = sessionValue.Value;
        if (session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            await cacheService.RemoveAsync(userCodeKey, cancellationToken).ConfigureAwait(false);
            return Result.Fail("expired_token", "user_code is expired.");
        }

        if (session.Status != OidcDeviceAuthorizationStatus.Pending)
        {
            return Result.Fail("invalid_state", "device authorization is no longer pending.");
        }

        return Result.Success(ToDescriptor(session));
    }

    public Task<Result> ApproveAsync(
        string userCode,
        string subject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult<Result>(Result.Fail("invalid_request", "subject is required."));
        }

        return DecideAsync(userCode, OidcDeviceAuthorizationStatus.Approved, subject.Trim(), cancellationToken);
    }

    public Task<Result> DenyAsync(
        string userCode,
        CancellationToken cancellationToken = default)
        => DecideAsync(userCode, OidcDeviceAuthorizationStatus.Denied, subject: null, cancellationToken);

    public async Task<Result<OAuthTokenEndpointResponse>> RedeemAsync(
        string deviceCode,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceCode) || deviceCode.Length > 512)
        {
            return Result.Fail("invalid_request", "device_code is required.");
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Fail("invalid_client", "client_id is required.");
        }

        var deviceCodeKey = new OidcDeviceAuthorizationCacheKey(HashValue(deviceCode.Trim()));
        var initialSession = await cacheService.GetAsync(deviceCodeKey, cancellationToken).ConfigureAwait(false);
        if (!initialSession.HasValue)
        {
            return Result.Fail("invalid_grant", "device_code is invalid or already redeemed.");
        }

        await using var sessionLock = await cacheService
            .AcquireLockAsync(deviceCodeKey, LockExpiration, cancellationToken)
            .ConfigureAwait(false);
        var sessionValue = await cacheService.GetAsync(deviceCodeKey, cancellationToken).ConfigureAwait(false);
        if (!sessionValue.HasValue)
        {
            return Result.Fail("invalid_grant", "device_code is invalid or already redeemed.");
        }

        var session = sessionValue.Value;
        if (!FixedTimeEquals(session.ClientId, clientId.Trim()))
        {
            return Result.Fail("invalid_grant", "device_code was not issued to this client.");
        }

        var now = timeProvider.GetUtcNow();
        if (session.ExpiresAtUtc <= now)
        {
            return Result.Fail("expired_token", "device_code is expired.");
        }

        if (session.Status == OidcDeviceAuthorizationStatus.Denied)
        {
            return Result.Fail("access_denied", "The end user denied the device authorization request.");
        }

        if (session.Status == OidcDeviceAuthorizationStatus.Redeemed)
        {
            return session.RedeemedResponse is null
                ? Result.Fail("invalid_grant", "device_code is already redeemed.")
                : Result.Success(session.RedeemedResponse);
        }

        if (session.Status == OidcDeviceAuthorizationStatus.Pending)
        {
            if (now < session.NextAllowedPollAtUtc)
            {
                session = session with
                {
                    PollingIntervalSeconds = checked(session.PollingIntervalSeconds + 5),
                    NextAllowedPollAtUtc = now.AddSeconds(session.PollingIntervalSeconds + 5)
                };
                await cacheService.SetAsync(
                    deviceCodeKey,
                    session,
                    CreateSessionCacheOptions(session),
                    cancellationToken).ConfigureAwait(false);
                return Result.Fail("slow_down", "The client is polling the token endpoint too quickly.");
            }

            session = session with
            {
                NextAllowedPollAtUtc = now.AddSeconds(session.PollingIntervalSeconds)
            };
            await cacheService.SetAsync(
                deviceCodeKey,
                session,
                CreateSessionCacheOptions(session),
                cancellationToken).ConfigureAwait(false);
            return Result.Fail("authorization_pending", "The end user has not completed device authorization.");
        }

        if (session.Status != OidcDeviceAuthorizationStatus.Approved || string.IsNullOrWhiteSpace(session.Subject))
        {
            return Result.Fail("invalid_grant", "device authorization state is invalid.");
        }

        OAuthTokenResponseIssuer? tokenResponseIssuer;
        try
        {
            tokenResponseIssuer = serviceProvider.GetService<OAuthTokenResponseIssuer>();
        }
        catch (InvalidOperationException)
        {
            tokenResponseIssuer = null;
        }

        if (tokenResponseIssuer is null)
        {
            return Result.Fail("server_error", "OAuth subject service is not registered.");
        }

        var response = await tokenResponseIssuer.IssueAsync(
            session.Subject,
            session.Scope,
            session.ClientId,
            idTokenAudience: session.ClientId,
            nonce: null,
            additionalAccessClaims: null,
            issueRefreshToken: session.Scopes.Contains(OAuthScope.OfflineAccess),
            cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Result.Fail("invalid_grant", "device authorization subject is invalid.");
        }

        var redeemed = session with
        {
            Status = OidcDeviceAuthorizationStatus.Redeemed,
            RedeemedResponse = response
        };
        await cacheService.SetAsync(
            deviceCodeKey,
            redeemed,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.Value.RedeemedDeviceCodeGracePeriod
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(response);
    }

    private async Task<Result> DecideAsync(
        string userCode,
        OidcDeviceAuthorizationStatus decision,
        string? subject,
        CancellationToken cancellationToken)
    {
        var sessionLookup = await ResolveSessionByUserCodeAsync(userCode, cancellationToken).ConfigureAwait(false);
        if (!sessionLookup.IsSuccess)
        {
            return Result.Fail(sessionLookup.ErrorCode, sessionLookup.Message);
        }

        var (userCodeKey, deviceCodeKey) = sessionLookup.Value;
        await using var sessionLock = await cacheService
            .AcquireLockAsync(deviceCodeKey, LockExpiration, cancellationToken)
            .ConfigureAwait(false);
        var sessionValue = await cacheService.GetAsync(deviceCodeKey, cancellationToken).ConfigureAwait(false);
        if (!sessionValue.HasValue)
        {
            await cacheService.RemoveAsync(userCodeKey, cancellationToken).ConfigureAwait(false);
            return Result.Fail("invalid_user_code", "user_code is invalid or expired.");
        }

        var session = sessionValue.Value;
        if (session.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            await cacheService.RemoveAsync(userCodeKey, cancellationToken).ConfigureAwait(false);
            return Result.Fail("expired_token", "user_code is expired.");
        }

        if (session.Status != OidcDeviceAuthorizationStatus.Pending)
        {
            return Result.Fail("invalid_state", "device authorization is no longer pending.");
        }

        var decided = session with
        {
            Status = decision,
            Subject = subject
        };
        await cacheService.SetAsync(
            deviceCodeKey,
            decided,
            CreateSessionCacheOptions(decided),
            cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(userCodeKey, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<(OidcDeviceUserCodeCacheKey UserCodeKey, OidcDeviceAuthorizationCacheKey DeviceCodeKey)>>
        ResolveSessionByUserCodeAsync(string userCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userCode) || userCode.Length > 64)
        {
            return Result.Fail("invalid_user_code", "user_code is required.");
        }

        var normalizedUserCode = NormalizeUserCode(userCode);
        if (string.IsNullOrWhiteSpace(normalizedUserCode))
        {
            return Result.Fail("invalid_user_code", "user_code is required.");
        }

        var userCodeKey = new OidcDeviceUserCodeCacheKey(HashValue(normalizedUserCode));
        var deviceCodeDigest = await cacheService.GetAsync(userCodeKey, cancellationToken).ConfigureAwait(false);
        if (!deviceCodeDigest.HasValue || string.IsNullOrWhiteSpace(deviceCodeDigest.Value))
        {
            return Result.Fail("invalid_user_code", "user_code is invalid or expired.");
        }

        return Result.Success((userCodeKey, new OidcDeviceAuthorizationCacheKey(deviceCodeDigest.Value)));
    }

    private DistributedCacheEntryOptions CreateSessionCacheOptions(OidcDeviceAuthorizationCacheValue session)
        => new()
        {
            AbsoluteExpiration = session.ExpiresAtUtc.Add(options.Value.ExpiredDeviceCodeRetention)
        };

    private static OAuthDeviceAuthorizationDescriptor ToDescriptor(OidcDeviceAuthorizationCacheValue session)
        => new()
        {
            UserCode = session.UserCode,
            ClientId = session.ClientId,
            ClientDisplayName = session.ClientDisplayName,
            ClientLogoUri = session.ClientLogoUri,
            Scopes = session.Scopes.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            ExpiresAtUtc = session.ExpiresAtUtc
        };

    private static string ResolveVerificationUri(OAuthAuthorizationServerOptions serverOptions)
        => string.IsNullOrWhiteSpace(serverOptions.DeviceVerificationUri)
            ? $"{serverOptions.Issuer.TrimEnd('/')}/device"
            : serverOptions.DeviceVerificationUri.Trim();

    private static string AddQueryString(string uri, string name, string value)
    {
        var separator = uri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{uri}{separator}{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
    }

    private static string CreateUserCode(int length)
    {
        var characters = new char[length];
        for (var index = 0; index < length; index++)
        {
            characters[index] = UserCodeAlphabet[RandomNumberGenerator.GetInt32(UserCodeAlphabet.Length)];
        }

        return string.Join('-', Enumerable.Range(0, (length + 3) / 4)
            .Select(chunk => new string(characters, chunk * 4, Math.Min(4, length - (chunk * 4)))));
    }

    private static string NormalizeUserCode(string? userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode))
        {
            return string.Empty;
        }

        var normalized = new StringBuilder(userCode.Length);
        foreach (var character in userCode)
        {
            var upper = char.ToUpperInvariant(character);
            if (UserCodeAlphabet.Contains(upper, StringComparison.Ordinal))
            {
                normalized.Append(upper);
            }
        }

        return normalized.ToString();
    }

    private static string HashValue(string value)
        => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}

internal enum OidcDeviceAuthorizationStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Redeemed = 3
}

internal sealed record OidcDeviceAuthorizationCacheValue
{
    public required string UserCode { get; init; }

    public required string ClientId { get; init; }

    public required string ClientDisplayName { get; init; }

    public string? ClientLogoUri { get; init; }

    public required string Scope { get; init; }

    [JsonIgnore]
    public IReadOnlySet<string> Scopes
        => Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    public required OidcDeviceAuthorizationStatus Status { get; init; }

    public string? Subject { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required int PollingIntervalSeconds { get; init; }

    public required DateTimeOffset NextAllowedPollAtUtc { get; init; }

    public OAuthTokenEndpointResponse? RedeemedResponse { get; init; }
}

internal sealed record OidcDeviceAuthorizationCacheKey(string DeviceCodeDigest)
    : CacheKey<OidcDeviceAuthorizationCacheValue>($"nof:oauth:device-code:{DeviceCodeDigest}");

internal sealed record OidcDeviceUserCodeCacheKey(string UserCodeDigest)
    : CacheKey<string>($"nof:oauth:user-code:{UserCodeDigest}");
