using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NOF.Hosting;
using NOF.Hosting.Extension.Authorization.Jwt;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public static partial class NOFJwtAuthorizationExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddJwtAuthority(Action<JwtAuthorityOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);

            builder.Services.ReplaceOrAddSingleton<IJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddSingleton<ISigningKeyService, SigningKeyService>();
            builder.Services.ReplaceOrAddScoped<JwtAuthorityService, JwtAuthorityService>();
            builder.Services.ReplaceOrAddSingleton<CachedJwksService, CachedJwksService>();
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
            builder.Services.TryAddScoped<IJwksService, HttpJwksService>();
            builder.Services.TryAddSingleton<CachedJwksService, CachedJwksService>();
            builder.Services.AddRequestInboundMiddleware<JwtResourceServerInboundMiddleware>();
            return builder;
        }
    }
}
