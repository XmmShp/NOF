using Microsoft.EntityFrameworkCore;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Test;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NOF.Infrastructure.Extension.Authorization.Jwt.Tests.Services;

public sealed class JwtAuthorityServiceTests
{
    private const string SigningKeyEncryptionKey = "jwt-signing-key-passphrase-for-tests";

    [Fact]
    public async Task GenerateJwtToken_ShouldIssueClaimsDrivenAccessAndRefreshTokens()
    {
        var builder = CreateAuthorityBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var client = scope.GetRequiredService<IJwtAuthorityServiceClient>();

        var generateResult = await client.GenerateJwtTokenAsync(
            new GenerateJwtTokenRequest
            {
                Audience = "nof-tests",
                AccessTokenExpiration = TimeSpan.FromMinutes(10),
                AccessClaims =
                [
                    new(ClaimTypes.NameIdentifier, "user-1"),
                    new(ClaimTypes.TenantId, "tenant-1"),
                    new(ClaimTypes.Permission, "printer.read"),
                    new(ClaimTypes.Permission, "printer.write")
                ],
                RefreshToken = new JwtRefreshTokenOptions
                {
                    Expiration = TimeSpan.FromHours(12),
                    Claims =
                    [
                        new(ClaimTypes.NameIdentifier, "user-1")
                    ]
                }
            });

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(generateResult.Value.AccessToken));
        Assert.NotNull(generateResult.Value.RefreshToken);
        Assert.NotEqual(default, generateResult.Value.RefreshToken.ExpiresAtUtc);

        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(generateResult.Value.AccessToken);
        Assert.False(string.IsNullOrWhiteSpace(accessToken.Header.Kid));
        Assert.Contains(accessToken.Claims, claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == "user-1");
        Assert.Contains(accessToken.Claims, claim => claim.Type == ClaimTypes.TenantId && claim.Value == "tenant-1");
        Assert.Equal(2, accessToken.Claims.Count(claim => claim.Type == ClaimTypes.Permission));

        var refreshToken = generateResult.Value.RefreshToken;
        Assert.NotNull(refreshToken);

        var validateResult = await client.ValidateJwtRefreshTokenAsync(
            new ValidateJwtRefreshTokenRequest
            {
                RefreshToken = refreshToken.Token
            });

        Assert.True(validateResult.IsSuccess, validateResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(validateResult.Value.TokenId));
        Assert.Contains(validateResult.Value.Claims, claim => claim.Key == ClaimTypes.JwtId);
        Assert.Contains(validateResult.Value.Claims, claim => claim.Key == ClaimTypes.NameIdentifier && claim.Value == "user-1");
        Assert.DoesNotContain(validateResult.Value.Claims, claim => claim.Key == ClaimTypes.TenantId);
    }

    [Fact]
    public async Task GenerateJwtToken_WithoutRefreshToken_ShouldOnlyIssueAccessToken()
    {
        var builder = CreateAuthorityBuilder();

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var client = scope.GetRequiredService<IJwtAuthorityServiceClient>();

        var generateResult = await client.GenerateJwtTokenAsync(
            new GenerateJwtTokenRequest
            {
                Audience = "nof-tests",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(ClaimTypes.NameIdentifier, "service-1")
                ]
            });

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(generateResult.Value.AccessToken));
        Assert.Null(generateResult.Value.RefreshToken);
    }

    [Fact]
    public async Task GenerateJwtToken_ShouldSerializeNumericDateClaimsAsNumbers()
    {
        var builder = CreateAuthorityBuilder();
        const string issuedAt = "1710000000";

        await using var host = await builder.BuildTestHostAsync();
        using var scope = host.CreateScope();
        var client = scope.GetRequiredService<IJwtAuthorityServiceClient>();

        var generateResult = await client.GenerateJwtTokenAsync(
            new GenerateJwtTokenRequest
            {
                Audience = "nof-tests",
                AccessTokenExpiration = TimeSpan.FromMinutes(5),
                AccessClaims =
                [
                    new(JwtRegisteredClaimNames.Sub, "user-1"),
                    new(JwtRegisteredClaimNames.Iat, issuedAt),
                    new("auth_time", issuedAt)
                ]
            });

        Assert.True(generateResult.IsSuccess, generateResult.Message);
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(generateResult.Value.AccessToken.Split('.')[1]));
        using var payload = JsonDocument.Parse(payloadJson);
        Assert.Equal(JsonValueKind.Number, payload.RootElement.GetProperty(JwtRegisteredClaimNames.Iat).ValueKind);
        Assert.Equal(JsonValueKind.Number, payload.RootElement.GetProperty("auth_time").ValueKind);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    private static NOFTestAppBuilder CreateAuthorityBuilder()
    {
        var builder = NOFTestAppBuilder.Create();
        builder.AddJwtAuthority(options =>
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
