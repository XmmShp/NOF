using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting;

namespace NOF.Infrastructure.Extension.Authentication;

public sealed class PersistedSigningKeyPersistenceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.ReplaceOrAddScoped<ISigningKeyService, PersistenceSigningKeyService>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INOFDbContextModelCreatingContributor, PersistedSigningKeyModelCreatingContributor>());

        return ValueTask.CompletedTask;
    }
}
