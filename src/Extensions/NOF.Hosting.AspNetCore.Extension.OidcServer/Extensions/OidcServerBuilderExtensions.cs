using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Hosting.AspNetCore.Extension.OidcServer;
using NOF.Infrastructure;

namespace NOF.Hosting;

public static partial class NOFOidcServerExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public OidcServerSelector AddOidcServer(Action<OAuthAuthorizationServerOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);
            builder.Services.ReplaceOrAddScoped<ISigningKeyService, PersistenceSigningKeyService>();
            builder.Services.TryAddScoped<IRevokedRefreshTokenRepository, PersistenceRevokedRefreshTokenRepository>();
            builder.Services.TryAddScoped<PersistenceOAuthClientRepository>();
            builder.Services.TryAddScoped<IOAuthClientRepository>(static serviceProvider =>
                serviceProvider.GetRequiredService<PersistenceOAuthClientRepository>());
            builder.Services.TryAddScoped<IOAuthTokenExchangeHandler, DefaultOAuthTokenExchangeHandler>();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, PersistedSigningKeyModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, RevokedRefreshTokenModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, OAuthClientModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RevokedRefreshTokenCleanupBackgroundService>());
            builder.Services.ReplaceOrAddScoped<LocalJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<IJwksService>(static serviceProvider => serviceProvider.GetRequiredService<LocalJwksService>());
            builder.Services.ReplaceOrAddScoped<ITokenService, TokenAuthorityService>();
            builder.Services.TryAddScoped<IOAuthServerRootEndpoint, DefaultOAuthServerRootEndpoint>();
            builder.Services.TryAddScoped<IOAuthMetadataEndpoint, DefaultOAuthMetadataEndpoint>();
            builder.Services.TryAddScoped<IOAuthJwksEndpoint, DefaultOAuthJwksEndpoint>();
            builder.Services.TryAddScoped<IOAuthAuthorizeEndpoint, DefaultOAuthAuthorizeEndpoint>();
            builder.Services.TryAddScoped<IOAuthTokenEndpoint, DefaultOAuthTokenEndpoint>();
            builder.Services.TryAddScoped<IOAuthRevokeEndpoint, DefaultOAuthRevokeEndpoint>();
            builder.Services.TryAddScoped<IOAuthIntrospectEndpoint, DefaultOAuthIntrospectEndpoint>();
            builder.Services.TryAddScoped<IOAuthUserInfoEndpoint, DefaultOAuthUserInfoEndpoint>();
            builder.Services.TryAddScoped<OAuthAuthorizationCodeIssuer>();
            builder.Services.AddHostedService<SigningKeyRotationBackgroundService>();
            builder.Services.AddOptions<OidcServerBootstrapOptions>();
            builder.Services.TryAddSingleton<OidcServerEndpointMappingState>();
            builder.Services.TryAddInitializationStep<OidcServerInitializationStep>();
            return new OidcServerSelector(builder);
        }
    }
}
