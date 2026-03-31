using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Annotation;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[AutoInject(Lifetime.Scoped, RegisterTypes = [typeof(JwtAuthorityService.GenerateJwtToken)])]
public sealed class GenerateJwtToken : JwtAuthorityService.GenerateJwtToken
{
    private readonly ISigningKeyService _signingKeyService;
    private readonly JwtAuthorityOptions _options;

    public GenerateJwtToken(ISigningKeyService signingKeyService, IOptions<JwtAuthorityOptions> options)
    {
        _signingKeyService = signingKeyService;
        _options = options.Value;
    }

    public Task<Result<GenerateJwtTokenResponse>> GenerateJwtTokenAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var refreshTokenId = Guid.NewGuid().ToString("N");

        var accessClaims = new List<Claim>
        {
            new(ClaimTypes.Subject, request.UserId),
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
        var signingKey = _signingKeyService.CurrentSigningKey.Key;
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var accessToken = tokenHandler.WriteToken(new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: request.Audience,
            claims: accessClaims,
            notBefore: now,
            expires: now.Add(request.AccessTokenExpiration),
            signingCredentials: signingCredentials));

        var refreshClaims = new[]
        {
            new Claim(ClaimTypes.JwtId, refreshTokenId),
            new Claim(ClaimTypes.Subject, request.UserId),
            new Claim(ClaimTypes.TenantId, request.TenantId)
        };

        var refreshToken = tokenHandler.WriteToken(new JwtSecurityToken(
            issuer: _options.Issuer,
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

        return Task.FromResult(Result.Success(new GenerateJwtTokenResponse(tokenPair)));
    }
}
