using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace NOF.Infrastructure.EntityFrameworkCore;

/// <summary>
/// A model finalizing convention that removes entities marked with <see cref="HostOnlyAttribute"/>
/// from the model. Runs after all OnModelCreating configurations are applied,
/// so user Fluent API configurations on host-only entities won't be disrupted.
/// </summary>
internal sealed class HostOnlyModelFinalizingConvention : IModelFinalizingConvention
{
    private readonly HashSet<Type> _additionalIgnoredTypes;

    public HostOnlyModelFinalizingConvention(Type[] additionalIgnoredTypes)
    {
        _additionalIgnoredTypes = new HashSet<Type>(additionalIgnoredTypes);
    }

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var entityTypes = modelBuilder.Metadata.GetEntityTypes().ToList();

        foreach (var entityType in entityTypes)
        {
            if (entityType.ClrType.IsDefined(typeof(HostOnlyAttribute), true)
                || _additionalIgnoredTypes.Contains(entityType.ClrType))
            {
                modelBuilder.Metadata.RemoveEntityType(entityType);
            }
        }
    }
}
