using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace NOF;

/// <summary>
/// Model customizer that ignores entities with HostOnlyAttribute in tenant contexts.
/// </summary>
public class IgnoreHostOnlyModelCustomizer
{
    public void Customize(ModelBuilder modelBuilder)
    {
        // Get all entity types in the model
        var entityTypes = modelBuilder.Model.GetEntityTypes().ToList();

        foreach (var clrType in entityTypes
                     .Select(entityType => entityType.ClrType)
                     .Where(t => t.IsDefined(typeof(HostOnlyAttribute))))
        {
            modelBuilder.Ignore(clrType);
        }
    }
}
