using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Hosting;
using NOF.Hosting.Extension.Authentication;
using NOF.Contract;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NOF.Application;

namespace NOF.Infrastructure.Extension.Authentication;

public sealed class AuthenticationResourceServerInboundMiddleware : IRequestInboundMiddleware,
    IAfter<InboundExceptionMiddleware>,
    IBefore<TenantInboundMiddleware>
{
    private readonly IUserContext _userContext;
    private readonly ResourceServerJwksCacheService _jwksCacheService;
    private readonly AuthenticationResourceServerOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<AuthenticationResourceServerInboundMiddleware> _logger;

    public AuthenticationResourceServerInboundMiddleware(
        IUserContext userContext,
        ResourceServerJwksCacheService jwksCacheService,
        IOptions<AuthenticationResourceServerOptions> jwtOptions,
        ILogger<AuthenticationResourceServerInboundMiddleware> logger)
    {
        _userContext = userContext;
        _jwksCacheService = jwksCacheService;
        _jwtOptions = jwtOptions.Value;
        _tokenHandler = new JwtSecurityTokenHandler();
        _logger = logger;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var tokenSources = GetTokenSources();
        var executionContext = context;
        if (tokenSources.Count == 0)
        {
            await next(executionContext, request, cancellationToken);
            return;
        }

        try
        {
            foreach (var source in tokenSources)
            {
                if (!TryGetToken(executionContext, source, out var token))
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
                        ? new AccessTokenIdentity(
                            CreateIdentity(token),
                            token,
                            downstreamPropagation: source.DownstreamPropagation)
                        : new AccessTokenIdentity(
                            identity,
                            token,
                            downstreamPropagation: source.DownstreamPropagation));
                    executionContext = (RequestInboundContext)executionContext.WithoutHeader(source.HeaderName);
                }
                catch (SecurityTokenExpiredException)
                {
                    _logger.LogDebug("access token expired from header {HeaderName}", source.HeaderName);
                }
                catch (SecurityTokenValidationException ex)
                {
                    _logger.LogDebug(ex, "access token validation failed from header {HeaderName}: {Message}", source.HeaderName, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during access token validation");
        }

        await next(executionContext, request, cancellationToken);
    }

    private List<AuthenticationTokenSourceOptions> GetTokenSources()
    {
        return _jwtOptions.Sources;
    }

    private static bool TryGetToken(Context context, AuthenticationTokenSourceOptions source, out string token)
    {
        token = string.Empty;

        if (!context.TryGetHeader(source.HeaderName, out var authHeader) || string.IsNullOrEmpty(authHeader))
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
