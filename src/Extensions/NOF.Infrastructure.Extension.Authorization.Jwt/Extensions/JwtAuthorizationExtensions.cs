using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Contract.Extension.Authorization.Jwt;
using NOF.Hosting;
using System.Text.Json;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public static partial class NOFJwtAuthorizationExtensions
{
    private static int _jsonInitialized;

    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddJwtAuthority(Action<JwtAuthorityOptions> configureOptions)
        {
            EnsureJsonRegistered();
            builder.Services.Configure(configureOptions);

            builder.AddApplicationPart(typeof(JwtAuthorityService).Assembly);
            builder.TryAddRegistrationStep<PersistedSigningKeyPersistenceRegistrationStep>();
            builder.TryAddRegistrationStep<RevokedRefreshTokenPersistenceRegistrationStep>();
            builder.Services.ReplaceOrAddScoped<IJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwtAuthorityServiceClient, LocalJwtAuthorityServiceClient>();
            builder.Services.AddHostedService<JwtKeyRotationBackgroundService>();

            return builder;
        }

        public INOFAppBuilder AddJwtResourceServer(Action<JwtResourceServerOptions> configureOptions)
        {
            EnsureJsonRegistered();
            builder.Services.Configure(configureOptions);

            builder.Services.AddHttpClient<IJwksService, HttpJwksService>();
            builder.Services.ReplaceOrAddSingleton<ResourceServerJwksCacheService, ResourceServerJwksCacheService>();
            builder.Services.AddRequestInboundMiddleware<JwtResourceServerInboundMiddleware>();
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
            options.TypeInfoResolverChain.Add(NOFJwtAuthorizationJsonSerializerContext.Default);
        });
    }
}
