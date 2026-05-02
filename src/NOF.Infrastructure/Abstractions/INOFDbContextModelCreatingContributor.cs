using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure;

public interface INOFDbContextModelCreatingContributor
{
    void Configure(ModelBuilder modelBuilder);
}
