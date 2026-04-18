using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed partial class JwtAuthorityService : RpcServer<IJwtAuthorityService>;

public sealed class GenerateJwtTokenHandler : JwtAuthorityService.GenerateJwtToken
{
    public override Task<Result<GenerateJwtTokenResponse>> HandleAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return ExecuteGenerateJwtTokenCoreAsync(ServiceProvider, request, cancellationToken);
    }

    private IServiceProvider ServiceProvider { get; }

    public GenerateJwtTokenHandler(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    private static Task<Result<GenerateJwtTokenResponse>> ExecuteGenerateJwtTokenCoreAsync(
        IServiceProvider serviceProvider,
        GenerateJwtTokenRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var signingKeyService = serviceProvider.GetRequiredService<ISigningKeyService>();
        var options = serviceProvider.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value;

        var now = DateTime.UtcNow;
        var refreshTokenId = Guid.NewGuid().ToString("N");

        var accessClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserId),
            new(ClaimTypes.TenantId, request.TenantId)
        };

        if (request.Permissions is { Length: > 0 })
        {
            accessClaims.AddRange(request.Permissions.Select(permission => new Claim(ClaimTypes.Permission, permission)));
        }

        if (request.CustomClaims is not null)
        {
            accessClaims.AddRange(request.CustomClaims.Select(pair => new Claim(pair.Key, pair.Value)));
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var signingKey = signingKeyService.CurrentSigningKey.Key;
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var accessToken = tokenHandler.WriteToken(new JwtSecurityToken(
            issuer: options.Issuer,
            audience: request.Audience,
            claims: accessClaims,
            notBefore: now,
            expires: now.Add(request.AccessTokenExpiration),
            signingCredentials: signingCredentials));

        var refreshClaims = new[]
        {
            new Claim(ClaimTypes.JwtId, refreshTokenId),
            new Claim(ClaimTypes.NameIdentifier, request.UserId),
            new Claim(ClaimTypes.TenantId, request.TenantId)
        };

        var refreshToken = tokenHandler.WriteToken(new JwtSecurityToken(
            issuer: options.Issuer,
            audience: request.Audience,
            claims: refreshClaims,
            notBefore: now,
            expires: now.Add(request.RefreshTokenExpiration),
            signingCredentials: signingCredentials));

        var tokenPair = new TokenPair
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = now.Add(request.AccessTokenExpiration),
            RefreshTokenExpiresAt = now.Add(request.RefreshTokenExpiration)
        };

        return Task.FromResult(Result.Success(new GenerateJwtTokenResponse
        {
            TokenPair = tokenPair
        }));
    }
}

public sealed class ValidateJwtRefreshTokenHandler : JwtAuthorityService.ValidateJwtRefreshToken
{
    private IServiceProvider ServiceProvider { get; }

    public ValidateJwtRefreshTokenHandler(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public override Task<Result<ValidateJwtRefreshTokenResponse>> HandleAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken)
        => ExecuteValidateJwtRefreshTokenCoreAsync(ServiceProvider, request, cancellationToken);

    private static async Task<Result<ValidateJwtRefreshTokenResponse>> ExecuteValidateJwtRefreshTokenCoreAsync(
        IServiceProvider serviceProvider,
        ValidateJwtRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var signingKeyService = serviceProvider.GetRequiredService<ISigningKeyService>();
        var revokedRefreshTokenRepository = serviceProvider.GetRequiredService<IRevokedRefreshTokenRepository>();
        var options = serviceProvider.GetRequiredService<IOptions<JwtAuthorityOptions>>().Value;
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeyService.AllKeys.Select(k => k.Key),
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(request.RefreshToken, validationParameters, out _);
            var tokenId = principal.FindFirst(ClaimTypes.JwtId)?.Value;
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = principal.FindFirst(ClaimTypes.TenantId)?.Value;

            if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tenantId))
            {
                return Result.Fail("400", "Invalid refresh token claims.");
            }

            if (await revokedRefreshTokenRepository.IsRevokedAsync(tokenId, cancellationToken).ConfigureAwait(false))
            {
                return Result.Fail("401", "Refresh token has been revoked.");
            }

            return Result.Success(new ValidateJwtRefreshTokenResponse
            {
                TokenId = tokenId,
                UserId = userId,
                TenantId = tenantId
            });
        }
        catch (Exception ex)
        {
            return Result.Fail("401", ex.Message);
        }
    }
}

public sealed class RevokeJwtRefreshTokenHandler : JwtAuthorityService.RevokeJwtRefreshToken
{
    private IServiceProvider ServiceProvider { get; }

    public RevokeJwtRefreshTokenHandler(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public override Task<Result> HandleAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken)
        => ExecuteRevokeJwtRefreshTokenCoreAsync(ServiceProvider, request, cancellationToken);

    private static async Task<Result> ExecuteRevokeJwtRefreshTokenCoreAsync(
        IServiceProvider serviceProvider,
        RevokeJwtRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var revokedRefreshTokenRepository = serviceProvider.GetRequiredService<IRevokedRefreshTokenRepository>();

        await revokedRefreshTokenRepository
            .RevokeAsync(request.TokenId, request.Expiration, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
