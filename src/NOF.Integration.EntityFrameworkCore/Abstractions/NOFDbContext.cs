using Microsoft.EntityFrameworkCore;

namespace NOF;

public record DbContextModelCreating(ModelBuilder Builder);

public abstract class NOFDbContext : DbContext
{
    protected NOFDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        EventDispatcher.Publish(new DbContextModelCreating(modelBuilder));
    }
}
