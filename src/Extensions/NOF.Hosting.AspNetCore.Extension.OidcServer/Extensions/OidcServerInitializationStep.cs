using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class OidcServerInitializationStep : IApplicationInitializationStep
{
    public TopologyComparison Compare(IApplicationInitializationStep other)
        => other.GetType().Name switch
        {
            "DbContextMigrationInitializationStep" => TopologyComparison.After,
            "RpcHttpEndpointResultWrappingInitializationStep" => TopologyComparison.After,
            _ => TopologyComparison.DoesNotMatter
        };

    public async Task ExecuteAsync(IHost app)
    {
        if (app is IEndpointRouteBuilder routeBuilder)
        {
            routeBuilder.MapOidcServer();
        }

        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();

        var bootstrapOptions = scope.ServiceProvider.GetRequiredService<IOptions<OidcServerBootstrapOptions>>().Value;
        if (bootstrapOptions.PublicClients.Count == 0)
        {
            return;
        }

        var clientService = scope.ServiceProvider.GetRequiredService<IOAuthClientManagementService>();
        foreach (var request in bootstrapOptions.PublicClients
                     .GroupBy(static client => client.ClientId, StringComparer.Ordinal)
                     .Select(static group => group.Last()))
        {
            var existing = await clientService.GetAsync(request.ClientId).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                continue;
            }

            if (!string.Equals(existing.ErrorCode, "not_found", StringComparison.Ordinal))
            {
                EnsureSucceeded(existing, request.ClientId);
            }

            var createResult = await clientService.CreateAsync(request).ConfigureAwait(false);
            EnsureSucceeded(createResult, request.ClientId);
        }
    }

    private static void EnsureSucceeded(IResult result, string clientId)
    {
        if (result.IsSuccess)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Failed to bootstrap OIDC public client '{clientId}': [{result.ErrorCode}] {result.Message}");
    }
}
