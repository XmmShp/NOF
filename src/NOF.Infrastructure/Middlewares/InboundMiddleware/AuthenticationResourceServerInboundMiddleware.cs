using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NOF.Infrastructure;

public sealed class AuthenticationResourceServerInboundMiddleware :
    IRequestInboundMiddleware,
    ICommandInboundMiddleware,
    INotificationInboundMiddleware
{
    public TopologyComparison Compare(IRequestInboundMiddleware other)
        => other switch
        {
            TracingInboundMiddleware => TopologyComparison.After,
            TenantInboundMiddleware => TopologyComparison.Before,
            _ => TopologyComparison.DoesNotMatter
        };

    public TopologyComparison Compare(ICommandInboundMiddleware other)
        => other switch
        {
            TracingInboundMiddleware => TopologyComparison.After,
            TenantInboundMiddleware => TopologyComparison.Before,
            _ => TopologyComparison.DoesNotMatter
        };

    public TopologyComparison Compare(INotificationInboundMiddleware other)
        => other switch
        {
            TracingInboundMiddleware => TopologyComparison.After,
            TenantInboundMiddleware => TopologyComparison.Before,
            _ => TopologyComparison.DoesNotMatter
        };

    private readonly IUserContext _userContext;
    private readonly ResourceServerJwksCacheService _jwksCacheService;
    private readonly IPermissionResolver _permissionResolver;
    private readonly AuthenticationResourceServerOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<AuthenticationResourceServerInboundMiddleware> _logger;

    public AuthenticationResourceServerInboundMiddleware(
        IUserContext userContext,
        ResourceServerJwksCacheService jwksCacheService,
        IPermissionResolver permissionResolver,
        IOptions<AuthenticationResourceServerOptions> jwtOptions,
        ILogger<AuthenticationResourceServerInboundMiddleware> logger)
    {
        _userContext = userContext;
        _jwksCacheService = jwksCacheService;
        _permissionResolver = permissionResolver;
        _jwtOptions = jwtOptions.Value;
        _tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        _logger = logger;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, object request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = await AuthenticateAsync(context, cancellationToken).ConfigureAwait(false);
        await next(executionContext, request, cancellationToken);
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, object message, CommandHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = await AuthenticateAsync(context, cancellationToken).ConfigureAwait(false);
        await next(executionContext, message, cancellationToken);
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, object message, NotificationHandlerDelegate next, CancellationToken cancellationToken)
    {
        var executionContext = await AuthenticateAsync(context, cancellationToken).ConfigureAwait(false);
        await next(executionContext, message, cancellationToken);
    }

    private async ValueTask<TContext> AuthenticateAsync<TContext>(TContext context, CancellationToken cancellationToken)
        where TContext : Context
    {
        var tokenSources = GetTokenSources();
        var executionContext = context;
        if (tokenSources.Count == 0)
        {
            return executionContext;
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
                    var validIssuer = await GetValidIssuerAsync(cancellationToken).ConfigureAwait(false);
                    var validationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = !string.IsNullOrEmpty(validIssuer),
                        ValidIssuer = validIssuer,
                        ValidateAudience = !string.IsNullOrEmpty(_jwtOptions.Audience),
                        ValidAudience = _jwtOptions.Audience,
                        ValidateLifetime = true,
                        IssuerSigningKeys = keys,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
                    var identity = principal.Identities.FirstOrDefault();
                    _userContext.User.AddIdentity(identity is null
                        ? new JwtClaimsIdentity(
                            CreateIdentity(CreateIdentity(token)),
                            token,
                            downstreamPropagation: source.DownstreamPropagation)
                        : new JwtClaimsIdentity(
                            CreateIdentity(identity),
                            token,
                            downstreamPropagation: source.DownstreamPropagation));
                    executionContext = (TContext)executionContext.WithoutItem(source.HeaderName);
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

        return executionContext;
    }

    private List<AuthenticationTokenSourceOptions> GetTokenSources()
    {
        return _jwtOptions.Sources;
    }

    private Task<string?> GetValidIssuerAsync(CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(_jwtOptions.Issuer)
            ? _jwksCacheService.GetIssuerAsync(cancellationToken)
            : Task.FromResult<string?>(_jwtOptions.Issuer);

    private static bool TryGetToken(Context context, AuthenticationTokenSourceOptions source, out string token)
    {
        token = string.Empty;

        if (!context.TryGetItem(source.HeaderName, out var authHeaderObject)
            || authHeaderObject is not string authHeader
            || string.IsNullOrEmpty(authHeader))
        {
            return false;
        }

        var prefix = string.IsNullOrEmpty(source.TokenType)
            ? string.Empty
            : source.TokenType + " ";
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

    private ClaimsIdentity CreateIdentity(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var resolvedIdentity = new ClaimsIdentity(identity);
        var existingPermissions = resolvedIdentity.FindAll(ClaimTypes.Permission)
            .Select(static claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in _permissionResolver.ResolvePermissions(resolvedIdentity.Claims.ToArray()))
        {
            if (string.IsNullOrWhiteSpace(permission) || !existingPermissions.Add(permission))
            {
                continue;
            }

            resolvedIdentity.AddClaim(new Claim(ClaimTypes.Permission, permission));
        }

        return resolvedIdentity;
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
