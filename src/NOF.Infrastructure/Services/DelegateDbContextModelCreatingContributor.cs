using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure;

internal sealed class DelegateDbContextModelCreatingContributor(Action<ModelBuilder> configure) : INOFDbContextModelCreatingContributor
{
    public void Configure(ModelBuilder modelBuilder)
        => configure(modelBuilder);
}
