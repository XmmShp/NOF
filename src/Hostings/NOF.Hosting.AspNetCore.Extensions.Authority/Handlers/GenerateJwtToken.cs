using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Hosting.AspNetCore.Extensions.Authority;

/// <summary>
/// Handler for generating JWT token pair requests.
/// </summary>
public class GenerateJwtToken : IRequestHandler<GenerateJwtTokenRequest, GenerateJwtTokenResponse>
{
    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ISigningKeyService _signingKeyService;

    public GenerateJwtToken(IOptions<JwtOptions> options, ISigningKeyService signingKeyService)
    {
        _options = options.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _signingKeyService = signingKeyService;
    }

    public Task<Result<GenerateJwtTokenResponse>> HandleAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var accessTokenExpires = now.Add(request.AccessTokenExpiration);
            var refreshTokenExpires = now.Add(request.RefreshTokenExpiration);

            // Generate unique token IDs
            var jti = Guid.NewGuid().ToString();
            var refreshTokenId = Guid.NewGuid().ToString();

            // Use the current signing key for both access and refresh tokens
            var currentKey = _signingKeyService.CurrentSigningKey;

            var accessClaims = new JwtClaims
            {
                Jti = jti,
                Sub = request.UserId,
                TenantId = request.TenantId,
                Roles = request.Roles?.ToList() ?? [],
                Permissions = request.Permissions?.ToList() ?? [],
                CustomClaims = request.CustomClaims ?? new Dictionary<string, string>(),
                Iat = now,
                Exp = accessTokenExpires,
                Iss = _options.Issuer,
                Aud = request.Audience
            };
            var accessToken = CreateToken(accessClaims, currentKey);

            // Create refresh token
            var refreshClaims = new JwtClaims
            {
                Jti = refreshTokenId,
                Sub = request.UserId,
                TenantId = request.TenantId,
                Iat = now,
                Exp = refreshTokenExpires,
                Iss = _options.Issuer,
                Aud = request.Audience
            };
            var refreshToken = CreateToken(refreshClaims, currentKey);

            var tokenPair = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpires,
                RefreshTokenExpiresAt = refreshTokenExpires,
                TokenType = NOFJwtConstants.TokenType
            };

            return Task.FromResult(Result.Success(new GenerateJwtTokenResponse(tokenPair)));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Result<GenerateJwtTokenResponse>>(Result.Fail(500, ex.Message));
        }
    }

    private string CreateToken(JwtClaims claims, ManagedSigningKey managedKey)
    {
        var claimList = new List<Claim>
        {
            new(NOFJwtConstants.ClaimTypes.JwtId, claims.Jti),
            new(NOFJwtConstants.ClaimTypes.Subject, claims.Sub),
            new(NOFJwtConstants.ClaimTypes.IssuedAt, new DateTimeOffset(claims.Iat).ToUnixTimeSeconds().ToString()),
            new(NOFJwtConstants.ClaimTypes.ExpiresAt, new DateTimeOffset(claims.Exp).ToUnixTimeSeconds().ToString()),
            new(NOFJwtConstants.ClaimTypes.Issuer, claims.Iss),
            new(NOFJwtConstants.ClaimTypes.Audience, claims.Aud)
        };

        // Add tenant ID if provided
        if (!string.IsNullOrEmpty(claims.TenantId))
        {
            claimList.Add(new Claim(NOFJwtConstants.ClaimTypes.TenantId, claims.TenantId));
        }

        // Add roles if provided
        if (claims.Roles is not null)
        {
            claimList.AddRange(claims.Roles.Select(role => new Claim(NOFJwtConstants.ClaimTypes.Role, role)));
        }

        // Add permissions if provided
        if (claims.Permissions is not null)
        {
            claimList.AddRange(claims.Permissions.Select(permission => new Claim(NOFJwtConstants.ClaimTypes.Permission, permission)));
        }

        // Add custom claims if provided
        claimList.AddRange(claims.CustomClaims.Select(kv => new Claim(kv.Key, kv.Value)));

        var signingCredentials = new SigningCredentials(managedKey.Key, NOFJwtConstants.Algorithm)
        {
            Key = { KeyId = managedKey.Kid }
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claimList),
            Expires = claims.Exp,
            Issuer = claims.Iss,
            Audience = claims.Aud,
            SigningCredentials = signingCredentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }
}
