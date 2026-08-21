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
            builder.Services.TryAddScoped<IOAuthClientRegistrationRepository>(static serviceProvider =>
                serviceProvider.GetRequiredService<PersistenceOAuthClientRepository>());
            builder.Services.TryAddScoped<IOAuthInitialAccessTokenHandler, DefaultOAuthInitialAccessTokenHandler>();
            builder.Services.TryAddScoped<IOAuthTokenExchangeHandler, DefaultOAuthTokenExchangeHandler>();
            builder.Services.TryAddScoped<IOAuthDeviceGrantService, CacheOAuthDeviceGrantService>();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, PersistedSigningKeyModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, RevokedRefreshTokenModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextModelCreatingContributor, OAuthClientModelCreatingContributor>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RevokedRefreshTokenCleanupBackgroundService>());
            builder.Services.ReplaceOrAddScoped<LocalJwksService, LocalJwksService>();
            builder.Services.ReplaceOrAddScoped<LocalAuthorizationServerMetadataService, LocalAuthorizationServerMetadataService>();

            builder.Services.ReplaceOrAddScoped<IJwksService>(static serviceProvider => serviceProvider.GetRequiredService<LocalJwksService>());
            builder.Services.ReplaceOrAddScoped<IAuthorizationServerMetadataService>(static serviceProvider =>
                serviceProvider.GetRequiredService<LocalAuthorizationServerMetadataService>());
            builder.Services.ReplaceOrAddScoped<ITokenService, TokenAuthorityService>();
            builder.Services.TryAddScoped<IOAuthServerRootEndpoint, DefaultOAuthServerRootEndpoint>();
            builder.Services.TryAddScoped<IOAuthMetadataEndpoint, DefaultOAuthMetadataEndpoint>();
            builder.Services.TryAddScoped<IOAuthJwksEndpoint, DefaultOAuthJwksEndpoint>();
            builder.Services.TryAddScoped<IOAuthAuthorizeEndpoint, DefaultOAuthAuthorizeEndpoint>();
            builder.Services.TryAddScoped<IOAuthDeviceAuthorizationEndpoint, DefaultOAuthDeviceAuthorizationEndpoint>();
            builder.Services.TryAddScoped<IOAuthDeviceVerificationEndpoint, DefaultOAuthDeviceVerificationEndpoint>();
            builder.Services.TryAddScoped<IOAuthTokenEndpoint, DefaultOAuthTokenEndpoint>();
            builder.Services.TryAddScoped<IOAuthRevokeEndpoint, DefaultOAuthRevokeEndpoint>();
            builder.Services.TryAddScoped<IOAuthIntrospectEndpoint, DefaultOAuthIntrospectEndpoint>();
            builder.Services.TryAddScoped<IOAuthUserInfoEndpoint, DefaultOAuthUserInfoEndpoint>();
            builder.Services.TryAddScoped<IOAuthClientRegistrationEndpoint, DefaultOAuthClientRegistrationEndpoint>();
            builder.Services.TryAddScoped<OAuthAuthorizationCodeIssuer>();
            builder.Services.TryAddScoped<OAuthTokenResponseIssuer>();
            builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
            builder.Services.AddHostedService<SigningKeyRotationBackgroundService>();
            builder.Services.AddOptions<OidcServerBootstrapOptions>();
            builder.Services.AddOptions<OAuthAuthorizationServerOptions>()
                .Validate(
                    static options => options.DeviceCodeExpiration > TimeSpan.Zero,
                    "DeviceCodeExpiration must be greater than zero.")
                .Validate(
                    static options => options.DevicePollingInterval >= TimeSpan.FromSeconds(5),
                    "DevicePollingInterval must be at least five seconds.")
                .Validate(
                    static options => options.DeviceUserCodeLength is >= 8 and <= 12,
                    "DeviceUserCodeLength must be between 8 and 12.")
                .Validate(
                    static options => options.RedeemedDeviceCodeGracePeriod > TimeSpan.Zero,
                    "RedeemedDeviceCodeGracePeriod must be greater than zero.")
                .Validate(
                    static options => options.ExpiredDeviceCodeRetention > TimeSpan.Zero,
                    "ExpiredDeviceCodeRetention must be greater than zero.")
                .Validate(
                    static options => string.IsNullOrWhiteSpace(options.DeviceVerificationUri)
                        || Uri.TryCreate(options.DeviceVerificationUri, UriKind.Absolute, out _),
                    "DeviceVerificationUri must be an absolute URI when configured.");
            builder.Services.TryAddSingleton<OidcServerEndpointMappingState>();
            builder.Services.TryAddInitializationStep<OidcServerInitializationStep>();
            return new OidcServerSelector(builder);
        }
    }
}
