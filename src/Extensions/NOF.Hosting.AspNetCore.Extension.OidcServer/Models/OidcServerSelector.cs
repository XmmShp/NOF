using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Hosting;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public readonly struct OidcServerSelector : INOFAppBuilder
{
    public INOFAppBuilder Builder { get; }

    public OidcServerSelector(INOFAppBuilder builder)
    {
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public OidcServerSelector AddPublicClient(
        string clientId,
        IEnumerable<string>? allowedScopes = null,
        string? displayName = null,
        IEnumerable<OAuthClientClaim>? accessTokenClaims = null,
        bool isEnabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var normalizedClientId = clientId.Trim();
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedClientId : displayName.Trim();
        var scopes = allowedScopes?
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .ToArray() ?? [];
        var claims = accessTokenClaims?
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type))
            .Select(static claim => new OAuthClientClaim(claim.Type.Trim(), claim.Value))
            .ToArray() ?? [];

        Builder.Services.Configure<OidcServerBootstrapOptions>(options =>
        {
            options.PublicClients.Add(new CreateOAuthClientRequest
            {
                ClientId = normalizedClientId,
                DisplayName = normalizedDisplayName,
                AllowedScopes = scopes,
                AccessTokenClaims = claims,
                ClientType = OAuthClientType.Public,
                IsEnabled = isEnabled
            });
        });

        return this;
    }

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null) where TContainerBuilder : notnull
        => Builder.ConfigureContainer(factory, configure);
    public IDictionary<object, object> Properties => Builder.Properties;
    public IConfigurationManager Configuration => Builder.Configuration;
    public IHostEnvironment Environment => Builder.Environment;
    public ILoggingBuilder Logging => Builder.Logging;
    public IMetricsBuilder Metrics => Builder.Metrics;
    public IServiceCollection Services => Builder.Services;
}
