using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClaimTypes = System.Security.Claims.ClaimTypes;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Handler for generating JWT token pair requests.
/// </summary>
public class GenerateJwtToken : IRequestHandler<GenerateJwtTokenRequest, GenerateJwtTokenResponse>
{
    private readonly JwtAuthorityOptions _authorityOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ISigningKeyService _signingKeyService;

    public GenerateJwtToken(IOptions<JwtAuthorityOptions> authorityOptions, ISigningKeyService signingKeyService)
    {
        _authorityOptions = authorityOptions.Value;
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
                Permissions = request.Permissions?.ToList() ?? [],
                CustomClaims = request.CustomClaims ?? new Dictionary<string, string>(),
                Iat = now,
                Exp = accessTokenExpires,
                Iss = _authorityOptions.Issuer,
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
                Iss = _authorityOptions.Issuer,
                Aud = request.Audience
            };
            var refreshToken = CreateToken(refreshClaims, currentKey);

            var tokenPair = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpires,
                RefreshTokenExpiresAt = refreshTokenExpires
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
            new(ClaimTypes.JwtId, claims.Jti),
            new(ClaimTypes.Subject, claims.Sub)
        };

        // Add tenant ID if provided
        if (!string.IsNullOrEmpty(claims.TenantId))
        {
            claimList.Add(new Claim(ClaimTypes.TenantId, claims.TenantId));
        }

        // Add permissions if provided
        if (claims.Permissions is not null)
        {
            claimList.AddRange(claims.Permissions.Select(permission => new Claim(ClaimTypes.Permission, permission)));
        }

        // Add custom claims if provided
        claimList.AddRange(claims.CustomClaims.Select(kv => new Claim(kv.Key, kv.Value)));

        var signingCredentials = new SigningCredentials(managedKey.Key, NOFJwtAuthorizationConstants.Jwt.Algorithm)
        {
            Key = { KeyId = managedKey.Kid }
        };

        // iss, aud, exp, iat are set via SecurityTokenDescriptor properties to avoid duplication
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claimList),
            IssuedAt = claims.Iat,
            Expires = claims.Exp,
            Issuer = claims.Iss,
            Audience = claims.Aud,
            SigningCredentials = signingCredentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }
}
