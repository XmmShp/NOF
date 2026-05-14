using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Hosting;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed class PersistedSigningKeyPersistenceRegistrationStep : IServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.ReplaceOrAddScoped<ISigningKeyService, PersistenceSigningKeyService>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<INOFDbContextModelCreatingContributor, PersistedSigningKeyModelCreatingContributor>());

        return ValueTask.CompletedTask;
    }
}
