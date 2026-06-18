using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed class PersistedSigningKeyPersistenceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    public ValueTask ExecuteAsync(IHostApplicationBuilder builder)
    {
        builder.Services.ReplaceOrAddScoped<ISigningKeyService, PersistenceSigningKeyService>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INOFDbContextModelCreatingContributor, PersistedSigningKeyModelCreatingContributor>());

        return ValueTask.CompletedTask;
    }
}
