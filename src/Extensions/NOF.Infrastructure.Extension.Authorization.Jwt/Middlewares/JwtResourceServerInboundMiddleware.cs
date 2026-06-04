using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;
using NOF.Hosting.Extension.Authorization.Jwt;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class JwtResourceServerInboundMiddleware : IRequestInboundMiddleware,
    IAfter<InboundExceptionMiddleware>,
    IBefore<TenantInboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly ResourceServerJwksCacheService _jwksCacheService;
    private readonly JwtResourceServerOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<JwtResourceServerInboundMiddleware> _logger;
    private readonly ITransparentInfos _executionContext;

    public JwtResourceServerInboundMiddleware(
        IUserContext userContext,
        ResourceServerJwksCacheService jwksCacheService,
        IOptions<JwtResourceServerOptions> jwtOptions,
        ILogger<JwtResourceServerInboundMiddleware> logger,
        ITransparentInfos executionContext)
    {
        _userContext = userContext;
        _jwksCacheService = jwksCacheService;
        _jwtOptions = jwtOptions.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _logger = logger;
        _executionContext = executionContext;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var tokenSources = GetTokenSources();
        if (tokenSources.Count == 0)
        {
            await next(cancellationToken);
            return;
        }

        try
        {
            foreach (var source in tokenSources)
            {
                if (!TryGetToken(source, out var token))
                {
                    continue;
                }

                var keys = await _jwksCacheService
                    .GetSecurityKeysAsync(GetTokenKid(token), cancellationToken)
                    .ConfigureAwait(false);
                if (keys.Count == 0)
                {
                    _logger.LogDebug("No JWKS keys available for JWT validation");
                    continue;
                }

                try
                {
                    var validationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = !string.IsNullOrEmpty(_jwtOptions.Issuer),
                        ValidIssuer = _jwtOptions.Issuer,
                        ValidateAudience = !string.IsNullOrEmpty(_jwtOptions.Audience),
                        ValidAudience = _jwtOptions.Audience,
                        ValidateLifetime = true,
                        IssuerSigningKeys = keys,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
                    var identity = principal.Identities.OfType<ClaimsIdentity>().FirstOrDefault();
                    _userContext.User.AddIdentity(identity is null
                        ? new JwtClaimsIdentity(
                            CreateIdentity(token),
                            token,
                            downstreamPropagation: source.DownstreamPropagation)
                        : new JwtClaimsIdentity(
                            identity,
                            token,
                            downstreamPropagation: source.DownstreamPropagation));
                    _executionContext.RemoveHeader(source.HeaderName);
                }
                catch (SecurityTokenExpiredException)
                {
                    _logger.LogDebug("JWT token expired from header {HeaderName}", source.HeaderName);
                }
                catch (SecurityTokenValidationException ex)
                {
                    _logger.LogDebug(ex, "JWT token validation failed from header {HeaderName}: {Message}", source.HeaderName, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during JWT token validation");
        }

        await next(cancellationToken);
    }

    private List<JwtResourceServerTokenSourceOptions> GetTokenSources()
    {
        return _jwtOptions.Sources;
    }

    private bool TryGetToken(JwtResourceServerTokenSourceOptions source, out string token)
    {
        token = string.Empty;

        if (!_executionContext.TryGetHeader(source.HeaderName, out var authHeader) || string.IsNullOrEmpty(authHeader))
        {
            return false;
        }

        var prefix = source.TokenType + " ";
        token = authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authHeader[prefix.Length..]
            : authHeader;

        return !string.IsNullOrEmpty(token);
    }

    private static ClaimsIdentity CreateIdentity(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
    }

    private string? GetTokenKid(string token)
    {
        if (!_tokenHandler.CanReadToken(token))
        {
            return null;
        }

        try
        {
            return _tokenHandler.ReadJwtToken(token).Header.Kid;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
