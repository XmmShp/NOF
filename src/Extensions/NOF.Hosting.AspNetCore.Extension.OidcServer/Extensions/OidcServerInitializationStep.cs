using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.AspNetCore;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OidcServerInitializationStep : IApplicationInitializationStep
{
    public TopologyComparison Compare(IApplicationInitializationStep other)
        => other is DaemonServiceResolutionInitializationStep
            ? TopologyComparison.After
            : other.GetType().Name == "DbContextMigrationInitializationStep"
                ? TopologyComparison.After
                : TopologyComparison.DoesNotMatter;

    public async Task ExecuteAsync(IHost app)
    {
        if (app is IEndpointRouteBuilder routeBuilder)
        {
            routeBuilder.MapOidcServer();
        }

        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var bootstrapOptions = scope.ServiceProvider.GetRequiredService<IOptions<OidcServerBootstrapOptions>>().Value;
        if (bootstrapOptions.PublicClients.Count == 0 && bootstrapOptions.ConfidentialClients.Count == 0)
        {
            return;
        }

        var clientRepository = scope.ServiceProvider.GetRequiredService<IOAuthClientRepository>();
        foreach (var request in bootstrapOptions.PublicClients
                     .Concat(bootstrapOptions.ConfidentialClients)
                     .GroupBy(static client => client.ClientId, StringComparer.Ordinal)
                     .Select(static group => group.Last()))
        {
            var existing = await clientRepository.GetAsync(request.ClientId).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                continue;
            }

            if (!string.Equals(existing.ErrorCode, "not_found", StringComparison.Ordinal))
            {
                EnsureSucceeded(
                    existing,
                    request.ClientId,
                    request.ClientType == OAuthClientType.Public ? "public" : "confidential");
            }

            var createResult = await clientRepository.CreateAsync(request).ConfigureAwait(false);
            EnsureSucceeded(
                createResult,
                request.ClientId,
                request.ClientType == OAuthClientType.Public ? "public" : "confidential");
        }
    }

    private static void EnsureSucceeded(IResult result, string clientId, string clientType)
    {
        if (result.IsSuccess)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Failed to bootstrap OIDC {clientType} client '{clientId}': [{result.ErrorCode}] {result.Message}");
    }
}
