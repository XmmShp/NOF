using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Application;
using NOF.Contract;
using NOF.Contract.Extension.Authorization.Jwt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class JwtAuthorityService : IJwtAuthorityService
{
    private readonly IOutboundPipelineExecutor _outboundPipeline;
    private readonly IExecutionContext _executionContext;
    private readonly IServiceProvider _serviceProvider;

    public JwtAuthorityService(
        IOutboundPipelineExecutor outboundPipeline,
        IExecutionContext executionContext,
        IServiceProvider serviceProvider)
    {
        _outboundPipeline = outboundPipeline;
        _executionContext = executionContext;
        _serviceProvider = serviceProvider;
    }

    public Task<Result<GenerateJwtTokenResponse>> GenerateJwtTokenAsync(GenerateJwtTokenRequest request, CancellationToken cancellationToken = default)
        => ExecuteRpcAsync(request, typeof(JwtAuthorityService), ExecuteGenerateJwtTokenCoreAsync, cancellationToken);

    public Task<Result<ValidateJwtRefreshTokenResponse>> ValidateJwtRefreshTokenAsync(ValidateJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
        => ExecuteRpcAsync(request, typeof(JwtAuthorityService), ExecuteValidateJwtRefreshTokenCoreAsync, cancellationToken);

    public Task<Result> RevokeJwtRefreshTokenAsync(RevokeJwtRefreshTokenRequest request, CancellationToken cancellationToken = default)
        => ExecuteRpcAsync(request, typeof(JwtAuthorityService), ExecuteRevokeJwtRefreshTokenCoreAsync, cancellationToken);

    private async Task<TResult> ExecuteRpcAsync<TRequest, TResult>(
        TRequest request,
        Type handlerType,
        Func<IServiceProvider, TRequest, CancellationToken, Task<TResult>> terminal,
        CancellationToken cancellationToken)
        where TRequest : notnull
    {
        var outboundContext = new OutboundContext
        {
            Message = request,
            Services = _serviceProvider
        };

        TResult? result = default;

        await _outboundPipeline.ExecuteAsync(outboundContext, async ct =>
        {
            await InboundHandlerInvoker.ExecuteHandlerAsync(
                _serviceProvider,
                request,
                handlerType,
                _executionContext,
                async (sp, ct2) =>
                {
                    result = await terminal(sp, request, ct2).ConfigureAwait(false);
                },
                ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return result!;
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

        return Task.FromResult(Result.Success(new GenerateJwtTokenResponse(tokenPair)));
    }

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

            return Result.Success(new ValidateJwtRefreshTokenResponse(tokenId, userId, tenantId));
        }
        catch (Exception ex)
        {
            return Result.Fail("401", ex.Message);
        }
    }

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
