using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class DelegateDbContextModelCreatingContributor(Action<ModelBuilder> configure)
    : INOFDbContextModelCreatingContributor
{
    public void Configure(ModelBuilder modelBuilder)
        => configure(modelBuilder);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(configure.Method.Module.ModuleVersionId);
        hashCode.Add(configure.Method.MetadataToken);

        if (configure.Target is not null)
        {
            hashCode.Add(configure.Target.GetType());
            hashCode.Add(RuntimeHelpers.GetHashCode(configure.Target));
        }

        return hashCode.ToHashCode();
    }
}
