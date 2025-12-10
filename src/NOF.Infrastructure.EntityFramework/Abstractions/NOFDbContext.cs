using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace NOF;

public abstract class NOFDbContext : DbContext
{
    protected NOFDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
