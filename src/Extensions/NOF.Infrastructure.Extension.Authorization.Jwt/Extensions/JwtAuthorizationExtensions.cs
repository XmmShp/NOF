using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Application;
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
            NOFJwtAuthorizationJsonRegistration.EnsureRegistered();
            builder.Services.Configure(configureOptions);

            builder.TryAddRegistrationStep<PersistedSigningKeyPersistenceRegistrationStep>();
            builder.TryAddRegistrationStep<RevokedRefreshTokenPersistenceRegistrationStep>();
            builder.Services.GetOrAddSingleton<RpcServerInfos>()
                .Add(new RpcServerRegistration(typeof(IJwtAuthorityService), typeof(JwtAuthorityService)));
            builder.Services.ReplaceOrAddScoped<JwtAuthorityService, JwtAuthorityService>();
            builder.Services.ReplaceOrAddTransient<JwtAuthorityService.GenerateJwtToken, GenerateJwtTokenHandler>();
            builder.Services.ReplaceOrAddTransient<JwtAuthorityService.ValidateJwtRefreshToken, ValidateJwtRefreshTokenHandler>();
            builder.Services.ReplaceOrAddTransient<JwtAuthorityService.RevokeJwtRefreshToken, RevokeJwtRefreshTokenHandler>();
            builder.Services.ReplaceOrAddScoped<IJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<LocalJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwtAuthorityServiceClient, LocalJwtAuthorityServiceClient>();
            builder.Services.AddHostedService<JwtKeyRotationBackgroundService>();

            return builder;
        }

        public INOFAppBuilder AddJwtResourceServer(Action<JwtResourceServerOptions> configureOptions, Action<JwtTokenPropagationOptions>? tokenPropagationOptions = null)
        {
            NOFJwtAuthorizationJsonRegistration.EnsureRegistered();
            builder.Services.Configure(configureOptions);

            builder.AddJwtTokenPropagation(tokenPropagationOptions);
            builder.Services.AddOptions<JwtTokenPropagationOptions>()
                .Configure<IOptions<JwtResourceServerOptions>>((propagation, resource) =>
                {
                    propagation.HeaderName = resource.Value.HeaderName;
                    propagation.TokenType = resource.Value.TokenType;
                });
            builder.Services.AddHttpClient(nameof(HttpJwksService));
            builder.Services.ReplaceOrAddSingleton<HttpJwksService>(sp =>
                new HttpJwksService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HttpJwksService)),
                    sp.GetRequiredService<IOptions<JwtResourceServerOptions>>()));
            builder.Services.ReplaceOrAddSingleton<IJwksService>(sp => sp.GetRequiredService<HttpJwksService>());
            builder.Services.ReplaceOrAddSingleton<ResourceServerJwksCacheService, ResourceServerJwksCacheService>();
            builder.Services.AddRequestInboundMiddleware<JwtResourceServerInboundMiddleware>();
            return builder;
        }
    }
}
