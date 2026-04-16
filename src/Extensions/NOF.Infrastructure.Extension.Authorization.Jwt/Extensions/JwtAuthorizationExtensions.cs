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
        public JwtAuthoritySelector AddJwtAuthority(Action<JwtAuthorityOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptions<JwtAuthorityOptions>();
            }

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JwtAuthorityOptions>, JwtAuthorityOptionsValidator>());
            builder.Services.ReplaceOrAddSingleton<ISigningKeyService, SigningKeyService>();
            builder.Services.ReplaceOrAddScoped<JwtAuthorityService, JwtAuthorityService>();
            builder.Services.ReplaceOrAddScoped<JwksService, JwksService>();
            builder.Services.ReplaceOrAddSingleton<IJwksProvider, JwksProvider>();
            builder.Services.AddHostedService<JwtKeyRotationBackgroundService>();

            return new JwtAuthoritySelector(builder);
        }

        public JwtAuthoritySelector AddJwtAuthority(string issuer)
            => builder.AddJwtAuthority(options => options.Issuer = issuer);

        public INOFAppBuilder AddJwtResourceServer(Action<JwtResourceServerOptions>? configureOptions = null)
        {
            if (configureOptions is not null)
            {
                builder.Services.Configure(configureOptions);
            }
            else
            {
                builder.Services.AddOptions<JwtResourceServerOptions>();
            }

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JwtResourceServerOptions>, JwtResourceServerOptionsValidator>());
            builder.AddJwtTokenPropagation();
            builder.Services.AddOptions<JwtTokenPropagationOptions>()
                .Configure<IOptions<JwtResourceServerOptions>>((propagation, resource) =>
                {
                    propagation.HeaderName = resource.Value.HeaderName;
                    propagation.TokenType = resource.Value.TokenType;
                });
            builder.Services.AddHttpClient<HttpJwksService>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<JwtResourceServerOptions>>().Value;
                if (Uri.TryCreate(options.JwksEndpoint, UriKind.Absolute, out var jwksUri))
                {
                    client.BaseAddress = new Uri(jwksUri.GetLeftPart(UriPartial.Authority));
                }
            });
            builder.Services.ReplaceOrAddSingleton<IJwksProvider, JwksProvider>();
            builder.Services.AddRequestInboundMiddleware<JwtResourceServerInboundMiddleware>();
            return builder;
        }
    }
}
