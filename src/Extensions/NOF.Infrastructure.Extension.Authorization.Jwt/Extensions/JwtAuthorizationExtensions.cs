using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting;
using NOF.Hosting.Extension.Authorization.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public static partial class NOFJwtAuthorizationExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddJwtAuthority(Action<JwtAuthorityOptions> configureOptions)
        {
            builder.AddApplicationPart(typeof(JwtAuthorityService).Assembly);
            builder.Services.Configure(configureOptions);

            builder.Services.TryAddScoped<IRevokedRefreshTokenRepository, CacheRevokedRefreshTokenRepository>();
            builder.Services.ReplaceOrAddSingleton<IJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddSingleton<ISigningKeyService, SigningKeyService>();
            builder.Services.ReplaceOrAddSingleton<CachedJwksService, CachedJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwtAuthorityServiceClient, LocalJwtAuthorityServiceClient>();
            builder.Services.AddHostedService<JwtKeyRotationBackgroundService>();

            return builder;
        }

        public INOFAppBuilder AddJwtResourceServer(Action<JwtResourceServerOptions> configureOptions, Action<JwtTokenPropagationOptions>? tokenPropagationOptions = null)
        {
            builder.Services.Configure(configureOptions);

            builder.AddJwtTokenPropagation(tokenPropagationOptions);
            builder.Services.AddOptions<JwtTokenPropagationOptions>()
                .Configure<IOptions<JwtResourceServerOptions>>((propagation, resource) =>
                {
                    propagation.HeaderName = resource.Value.HeaderName;
                    propagation.TokenType = resource.Value.TokenType;
                });
            builder.Services.AddHttpClient<HttpJwksService>();
            builder.Services.TryAddTransient<IJwksService, HttpJwksService>();
            builder.Services.ReplaceOrAddSingleton<CachedJwksService, CachedJwksService>();
            builder.Services.AddRequestInboundMiddleware<JwtResourceServerInboundMiddleware>();
            return builder;
        }
    }
}
