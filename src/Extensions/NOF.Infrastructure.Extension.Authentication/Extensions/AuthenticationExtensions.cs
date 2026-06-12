using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Hosting;
using System.Text.Json;

namespace NOF.Infrastructure.Extension.Authentication;

public static partial class NOFAuthenticationExtensions
{
    private static int _jsonInitialized;

    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddAuthenticationAuthority(Action<AuthenticationAuthorityOptions> configureOptions)
        {
            EnsureJsonRegistered();
            builder.Services.Configure(configureOptions);

            builder.TryAddRegistrationStep<PersistedSigningKeyPersistenceRegistrationStep>();
            builder.TryAddRegistrationStep<RevokedRefreshTokenPersistenceRegistrationStep>();
            builder.Services.ReplaceOrAddScoped<LocalJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwksService>(static serviceProvider => serviceProvider.GetRequiredService<LocalJwksService>());
            builder.Services.ReplaceOrAddScoped<ITokenService, TokenAuthorityService>();
            builder.Services.AddHostedService<SigningKeyRotationBackgroundService>();

            return builder;
        }

        public INOFAppBuilder AddAuthenticationResourceServer(Action<AuthenticationResourceServerOptions> configureOptions)
        {
            EnsureJsonRegistered();
            builder.Services.Configure(configureOptions);

            builder.Services.AddHttpClient<HttpJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwksService>(static serviceProvider =>
                serviceProvider.GetService<LocalJwksService>() is IJwksService localJwksService
                    ? localJwksService
                    : serviceProvider.GetRequiredService<HttpJwksService>());
            builder.Services.ReplaceOrAddSingleton<ResourceServerJwksCacheService, ResourceServerJwksCacheService>();
            builder.Services.AddRequestInboundMiddleware<AuthenticationResourceServerInboundMiddleware>();
            return builder;
        }

        public INOFAppBuilder AddOAuthAuthorizationServer(Action<OAuthAuthorizationServerOptions> configureOptions)
        {
            EnsureJsonRegistered();
            builder.Services.Configure(configureOptions);
            builder.AddApplicationPart(typeof(OAuthAuthorizationServerService).Assembly);
            builder.Services.TryAddScoped<IOAuthAuthorizationCodeService, OAuthAuthorizationCodeService>();
            return builder;
        }
    }

    private static void EnsureJsonRegistered()
    {
        if (Interlocked.Exchange(ref _jsonInitialized, 1) == 1)
        {
            return;
        }

        JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        {
            options.TypeInfoResolverChain.Add(NOFAuthenticationJsonSerializerContext.Default);
        });
    }
}
