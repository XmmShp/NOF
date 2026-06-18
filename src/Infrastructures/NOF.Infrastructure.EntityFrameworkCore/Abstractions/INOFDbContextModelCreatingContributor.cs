using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure.EntityFrameworkCore;

public interface INOFDbContextModelCreatingContributor
{
    void Configure(ModelBuilder modelBuilder);
}
