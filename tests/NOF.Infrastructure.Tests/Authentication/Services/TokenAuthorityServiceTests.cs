using Microsoft.EntityFrameworkCore;
using NOF.Hosting;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Infrastructure.EntityFrameworkCore;
using NOF.Test;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NOF.Infrastructure.Tests.Authentication.Services;

public sealed class TokenAuthorityServiceTests
{
    private const string SigningKeyEncryptionKey = "jwt-signing-key-passphrase-for-tests";

    [Fact]
    public async Task IssueToken_ShouldIssueClaimsDrivenAccessAndRefreshTokens()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "web-client",
                AccessTokenExpiration = TimeSpan.FromMinutes(10),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1"),
                    new(ClaimTypes.TenantId, "tenant-1"),
                    new(ClaimTypes.Permission, "printer.read"),
                    new(ClaimTypes.Permission, "printer.write")
                ],
                RefreshToken = new RefreshTokenOptions
                {
                    Expiration = TimeSpan.FromHours(12),
                    Claims =
                    [
                        new(JwtRegisteredClaimNames.Sub, "user-1")
                    ]
                }
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(generateResult.Value.AccessToken));
        Assert.NotNull(generateResult.Value.RefreshToken);
        Assert.NotEqual(default, generateResult.Value.RefreshToken.ExpiresAtUtc);

        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(generateResult.Value.AccessToken);
        Assert.False(string.IsNullOrWhiteSpace(accessToken.Header.Kid));
        Assert.Equal("at+jwt", accessToken.Header.Typ);
        Assert.Contains(accessToken.Claims, claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == "user-1");
        Assert.Contains(accessToken.Claims, claim => claim.Type == OAuthClaimTypes.ClientId && claim.Value == "web-client");
        Assert.Contains(accessToken.Claims, claim => claim.Type == JwtRegisteredClaimNames.Iat);
        Assert.Contains(accessToken.Claims, claim => claim.Type == JwtRegisteredClaimNames.Jti);
        Assert.Contains(accessToken.Claims, claim => claim.Type == ClaimTypes.TenantId && claim.Value == "tenant-1");
        Assert.Equal(2, accessToken.Claims.Count(claim => claim.Type == ClaimTypes.Permission));

        var refreshToken = generateResult.Value.RefreshToken;
        Assert.NotNull(refreshToken);
        var refreshJwt = new JwtSecurityTokenHandler().ReadJwtToken(refreshToken.Token);
        Assert.Equal("JWT", refreshJwt.Header.Typ);
        Assert.Contains(refreshJwt.Claims, claim => claim.Type == OAuthClaimTypes.ClientId && claim.Value == "web-client");

        var validateResult = await tokenService.ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest
            {
                RefreshToken = refreshToken.Token
            },
            CancellationToken.None);

        Assert.True(validateResult.IsSuccess, validateResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(validateResult.Value.TokenId));
        Assert.Contains(validateResult.Value.Claims, claim => claim.Type == JwtRegisteredClaimNames.Jti);
        Assert.Contains(validateResult.Value.Claims, claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == "user-1");
        Assert.DoesNotContain(validateResult.Value.Claims, claim => claim.Type == ClaimTypes.TenantId);
    }

    [Fact]
    public async Task IssueToken_WithoutRefreshToken_ShouldOnlyIssueAccessToken()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "service-1",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "service-1")
                ]
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(generateResult.Value.AccessToken));
        Assert.Null(generateResult.Value.RefreshToken);
    }

    [Fact]
    public async Task IssueToken_ShouldSerializeNumericDateClaimsAsNumbers()
    {
        var builder = CreateOidcServerBuilder();
        const string issuedAt = "1710000000";

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "web-client",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1"),
                    TokenClaim.Integer64(JwtRegisteredClaimNames.Iat, long.Parse(issuedAt)),
                    new("auth_time", issuedAt)
                ]
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(generateResult.Value.AccessToken.Split('.')[1]));
        using var payload = JsonDocument.Parse(payloadJson);
        Assert.Equal(JsonValueKind.Number, payload.RootElement.GetProperty(JwtRegisteredClaimNames.Iat).ValueKind);
        Assert.Equal(JsonValueKind.Number, payload.RootElement.GetProperty("auth_time").ValueKind);
    }

    [Fact]
    public async Task IssueToken_ShouldSerializeExplicitArrayClaimsAsJsonArrays()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "web-client",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1"),
                    TokenClaim.Array(OAuthClaimTypes.Groups, "engineering", "ops")
                ]
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(generateResult.Value.AccessToken.Split('.')[1]));
        using var payload = JsonDocument.Parse(payloadJson);
        var groups = payload.RootElement.GetProperty(OAuthClaimTypes.Groups);

        Assert.Equal(JsonValueKind.Array, groups.ValueKind);
        Assert.Equal(["engineering", "ops"], groups.EnumerateArray().Select(static item => item.GetString()!).ToArray());
    }

    [Fact]
    public async Task IssueToken_ShouldSerializeJsonClaimsAsJsonObjects()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "service-a",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1"),
                    TokenClaim.Json(OAuthClaimTypes.Actor, """{"sub":"client:order-service","act":{"sub":"client:web-app"}}""")
                ]
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(generateResult.Value.AccessToken.Split('.')[1]));
        using var payload = JsonDocument.Parse(payloadJson);
        var actor = payload.RootElement.GetProperty(OAuthClaimTypes.Actor);

        Assert.Equal(JsonValueKind.Object, actor.ValueKind);
        Assert.Equal("client:order-service", actor.GetProperty(OAuthClaimTypes.Subject).GetString());
        Assert.Equal("client:web-app", actor.GetProperty(OAuthClaimTypes.Actor).GetProperty(OAuthClaimTypes.Subject).GetString());
    }

    [Fact]
    public async Task IssueToken_WithoutClientId_ShouldFail()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = string.Empty,
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1")
                ]
            },
            CancellationToken.None);

        Assert.False(generateResult.IsSuccess);
        Assert.Equal("400", generateResult.ErrorCode);
    }

    [Fact]
    public async Task ValidateRefreshToken_ShouldFail_WhenAudienceDoesNotMatch()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "web-client",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims = [new(JwtRegisteredClaimNames.Sub, "user-1")],
                RefreshToken = new RefreshTokenOptions
                {
                    Expiration = TimeSpan.FromMinutes(10),
                    Claims = [new(JwtRegisteredClaimNames.Sub, "user-1")]
                }
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        Assert.NotNull(generateResult.Value.RefreshToken);

        var validateResult = await tokenService.ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest
            {
                RefreshToken = generateResult.Value.RefreshToken.Token,
                Audience = "other-audience"
            },
            CancellationToken.None);

        Assert.False(validateResult.IsSuccess);
    }

    [Fact]
    public async Task IntrospectToken_ShouldReturnActiveAccessToken()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "service-a",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1"),
                    new(OAuthClaimTypes.Scope, "jobs.read")
                ]
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);

        var introspectResult = await tokenService.IntrospectTokenAsync(
            new IntrospectTokenRequest
            {
                Token = generateResult.Value.AccessToken,
                TokenTypeHint = OAuthTokenTypes.AccessToken,
                Audience = "nof-tests"
            },
            CancellationToken.None);

        Assert.True(introspectResult.IsSuccess, introspectResult.Message);
        Assert.True(introspectResult.Value.Active);
        Assert.Equal(OAuthTokenTypes.AccessToken, introspectResult.Value.TokenType);
        Assert.Contains(introspectResult.Value.Claims, claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == "user-1");
        Assert.Contains(introspectResult.Value.Claims, claim => claim.Type == OAuthClaimTypes.ClientId && claim.Value == "service-a");
        Assert.Contains(introspectResult.Value.Claims, claim => claim.Type == OAuthClaimTypes.Scope && claim.Value == "jobs.read");
    }

    [Fact]
    public async Task IntrospectToken_ShouldReturnExplicitArrayClaimsAsSingleMultiValueClaim()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "web-client",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1"),
                    TokenClaim.Array(OAuthClaimTypes.Groups, "engineering", "ops")
                ]
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);

        var introspectResult = await tokenService.IntrospectTokenAsync(
            new IntrospectTokenRequest
            {
                Token = generateResult.Value.AccessToken,
                TokenTypeHint = OAuthTokenTypes.AccessToken,
                Audience = "nof-tests"
            },
            CancellationToken.None);

        Assert.True(introspectResult.IsSuccess, introspectResult.Message);
        Assert.True(introspectResult.Value.Active);

        var groupsClaim = Assert.Single(introspectResult.Value.Claims, static claim => claim.Type == OAuthClaimTypes.Groups);
        Assert.Null(groupsClaim.Value);
        Assert.NotNull(groupsClaim.Values);
        Assert.Equal(["engineering", "ops"], groupsClaim.Values);
    }

    [Fact]
    public async Task IntrospectToken_ShouldReturnInactiveForRevokedRefreshToken()
    {
        var builder = CreateOidcServerBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var tokenService = scope.GetRequiredService<ITokenService>();

        var generateResult = await tokenService.IssueTokenAsync(
            new IssueTokenRequest
            {
                Audience = "nof-tests",
                ClientId = "public-app",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims = [new(JwtRegisteredClaimNames.Sub, "user-1")],
                RefreshToken = new RefreshTokenOptions
                {
                    Expiration = TimeSpan.FromMinutes(10),
                    Claims =
                    [
                        new(JwtRegisteredClaimNames.Sub, "user-1")
                    ]
                }
            },
            CancellationToken.None);

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        Assert.NotNull(generateResult.Value.RefreshToken);

        var validateResult = await tokenService.ValidateRefreshTokenAsync(
            new ValidateRefreshTokenRequest
            {
                RefreshToken = generateResult.Value.RefreshToken.Token,
                Audience = "nof-tests"
            },
            CancellationToken.None);

        Assert.True(validateResult.IsSuccess, validateResult.Message);

        var revokeResult = await tokenService.RevokeRefreshTokenAsync(
            new RevokeRefreshTokenRequest
            {
                TokenId = validateResult.Value.TokenId,
                Expiration = TimeSpan.FromMinutes(10)
            },
            CancellationToken.None);

        Assert.True(revokeResult.IsSuccess, revokeResult.Message);

        var introspectResult = await tokenService.IntrospectTokenAsync(
            new IntrospectTokenRequest
            {
                Token = generateResult.Value.RefreshToken.Token,
                TokenTypeHint = OAuthTokenTypes.RefreshToken,
                Audience = "nof-tests"
            },
            CancellationToken.None);

        Assert.True(introspectResult.IsSuccess, introspectResult.Message);
        Assert.False(introspectResult.Value.Active);
        Assert.Equal(OAuthTokenTypes.RefreshToken, introspectResult.Value.TokenType);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    private static NOFTestAppBuilder CreateOidcServerBuilder()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddOidcServer(options =>
        {
            options.Issuer = "https://issuer.local";
            options.SigningKeyEncryptionKey = SigningKeyEncryptionKey;
        });
        builder.UseDbContext<NOFDbContext>()
            .WithConnectionString($"Data Source=nof-jwt-tests-{Guid.NewGuid():N}-{{tenantId}};Mode=Memory;Cache=Shared")
            .WithOptions(static (optionsBuilder, databaseConnectionString) => optionsBuilder.UseSqlite(databaseConnectionString));

        return builder;
    }
}
